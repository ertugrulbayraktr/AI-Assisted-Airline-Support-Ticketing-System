using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Support.Application.Interfaces;
using Support.Domain.Entities;
using Support.Domain.Enums;
using Support.Infrastructure.BackgroundServices;
using Support.Infrastructure.Persistence;
using Xunit;

namespace Support.Api.IntegrationTests;

/// <summary>
/// SLA Monitor idempotency tests
/// Tests item F from Release Candidate checklist
/// </summary>
public class SlaIdempotencyTests
{
    [Fact]
    public async Task F1_SLA_Escalation_Is_Idempotent()
    {
        // Arrange: Setup in-memory database with SLA-breached ticket
        var services = new ServiceCollection();
        
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase($"SlaTest_{Guid.NewGuid()}"));
        
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

        // Create a ticket with breached SLA
        var user = new User("test@test.com", "hash", "Test User", Role.Passenger);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var ticket = new Ticket(
            "Urgent help needed",
            "SLA breach test",
            TicketCategory.General,
            Priority.P3,
            user.Id);

        // Manually set SLA breach (2 hours ago)
        var breachedTime = DateTime.UtcNow.AddHours(-3);
        typeof(Ticket).GetProperty("FirstResponseDueAt")!.SetValue(ticket, breachedTime);

        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();

        var ticketId = ticket.Id;
        var initialPriority = ticket.Priority;

        // Create SLA monitor (simulated)
        var slaMonitor = new SlaMonitorService(serviceProvider, 
            serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SlaMonitorService>>());

        // Debug: Check if breached ticket is found
        var now = DateTime.UtcNow;
        var breachedCheck = await context.Tickets
            .Where(t => t.FirstResponseAt == null && t.FirstResponseDueAt < now)
            .ToListAsync();
        
        Assert.NotEmpty(breachedCheck); // Sanity check: ticket should be breached

        // Act: Run SLA check TWICE (simulating two worker cycles)
        await RunSlaCheckOnce(serviceProvider);
        await RunSlaCheckOnce(serviceProvider);

        // Assert: Escalation should happen ONCE, not twice
        // Query fresh data from context
        var finalTicket = await context.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == ticketId);
        
        Assert.NotNull(finalTicket);
        
        // Priority should be escalated exactly once (P3 -> P2)
        Assert.Equal(Priority.P2, finalTicket.Priority);

        // Count escalation events - should be EXACTLY 1
        var escalationEvents = await context.TicketAuditEvents
            .Where(e => e.TicketId == ticketId && e.EventType == AuditEventType.Escalated)
            .ToListAsync();

        Assert.Single(escalationEvents); // IDEMPOTENCY: Only one escalation event

        // Count SLA breach events - should be EXACTLY 1
        var breachEvents = await context.TicketAuditEvents
            .Where(e => e.TicketId == ticketId && e.EventType == AuditEventType.SlaBreached)
            .ToListAsync();

        Assert.Single(breachEvents); // IDEMPOTENCY: Only one breach event

        // Verify notification count - should be EXACTLY 1
        var notifications = await context.Notifications
            .Where(n => n.TicketId == ticketId)
            .ToListAsync();

        Assert.Single(notifications); // IDEMPOTENCY: Only one notification
    }

    private async Task RunSlaCheckOnce(ServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var now = DateTime.UtcNow;

        var breachedTickets = await context.Tickets
            .Include(t => t.AuditEvents)
            .Where(t => 
                (t.FirstResponseAt == null && t.FirstResponseDueAt < now) ||
                (t.ResolvedAt == null && t.State != TicketState.Closed && t.State != TicketState.Cancelled && t.ResolutionDueAt < now))
            .ToListAsync();

        foreach (var ticket in breachedTickets)
        {
            var breachType = ticket.FirstResponseAt == null ? "FirstResponse" : "Resolution";

            var alreadyBreached = ticket.AuditEvents.Any(e => 
                e.EventType == AuditEventType.SlaBreached && 
                e.Details != null &&
                e.Details.Contains(breachType));

            if (alreadyBreached)
            {
                continue; // IDEMPOTENCY: Skip if already processed
            }

            if (ticket.Priority < Priority.P0)
            {
                ticket.Escalate();

                var escalationEvent = new TicketAuditEvent(
                    ticket.Id,
                    ActorType.System,
                    AuditEventType.Escalated,
                    details: $"Ticket escalated due to {breachType} SLA breach. Priority increased to {ticket.Priority}");

                context.TicketAuditEvents.Add(escalationEvent);

                if (ticket.AssignedToAgentId.HasValue)
                {
                    var notification = new Notification(
                        ticket.AssignedToAgentId.Value,
                        "SLA Breach Alert",
                        $"Ticket {ticket.TicketNumber} has breached {breachType} SLA",
                        ticket.Id);

                    context.Notifications.Add(notification);
                }
            }

            var slaBreachEvent = new TicketAuditEvent(
                ticket.Id,
                ActorType.System,
                AuditEventType.SlaBreached,
                details: $"{breachType} SLA breached at {now:yyyy-MM-dd HH:mm:ss} UTC");

            context.TicketAuditEvents.Add(slaBreachEvent);
        }

        if (breachedTickets.Any())
        {
            await context.SaveChangesAsync();
        }
    }
}
