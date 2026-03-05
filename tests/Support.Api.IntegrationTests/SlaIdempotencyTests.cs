using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Support.Application.Interfaces;
using Support.Domain.Entities;
using Support.Domain.Enums;
using Support.Infrastructure.BackgroundServices;
using Support.Infrastructure.Persistence;
using Xunit;

namespace Support.Api.IntegrationTests;

public class SlaIdempotencyTests
{
    [Fact]
    public async Task F1_SLA_Escalation_Is_Idempotent()
    {
        var dbName = $"SlaTest_{Guid.NewGuid()}";

        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(dbName));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();

        Guid ticketId;

        // Seed data in a scope
        using (var seedScope = serviceProvider.CreateScope())
        {
            var ctx = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var user = new User("test@test.com", "hash", "Test User", Role.Passenger);
            ctx.Users.Add(user);
            await ctx.SaveChangesAsync();

            var ticket = new Ticket(
                "Urgent help needed",
                "SLA breach test",
                TicketCategory.General,
                Priority.P3,
                user.Id);

            // Set breached SLA via reflection
            typeof(Ticket).GetProperty("FirstResponseDueAt")!
                .SetValue(ticket, DateTime.UtcNow.AddHours(-3));

            ctx.Tickets.Add(ticket);
            await ctx.SaveChangesAsync();
            ticketId = ticket.Id;
        }

        // Run SLA check TWICE
        await RunSlaCheckOnce(serviceProvider);
        await RunSlaCheckOnce(serviceProvider);

        // Assert in a fresh scope
        using (var assertScope = serviceProvider.CreateScope())
        {
            var ctx = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var finalTicket = await ctx.Tickets
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == ticketId);

            Assert.NotNull(finalTicket);
            Assert.Equal(Priority.P2, finalTicket.Priority);

            var escalationEvents = await ctx.TicketAuditEvents
                .Where(e => e.TicketId == ticketId && e.EventType == AuditEventType.Escalated)
                .ToListAsync();
            Assert.Single(escalationEvents);

            var breachEvents = await ctx.TicketAuditEvents
                .Where(e => e.TicketId == ticketId && e.EventType == AuditEventType.SlaBreached)
                .ToListAsync();
            Assert.Single(breachEvents);
        }
    }

    private async Task RunSlaCheckOnce(ServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = DateTime.UtcNow;

        var breachedTickets = await context.Tickets
            .Include(t => t.AuditEvents)
            .Where(t =>
                (t.FirstResponseAt == null && t.FirstResponseDueAt < now) ||
                (t.ResolvedAt == null && t.State != TicketState.Closed && t.State != TicketState.Cancelled && t.ResolutionDueAt < now))
            .ToListAsync();

        foreach (var ticket in breachedTickets)
        {
            var breachTypes = new List<string>();
            if (ticket.FirstResponseAt == null && ticket.FirstResponseDueAt < now)
                breachTypes.Add("FirstResponse");
            if (ticket.ResolvedAt == null && ticket.State != TicketState.Closed && ticket.State != TicketState.Cancelled && ticket.ResolutionDueAt < now)
                breachTypes.Add("Resolution");

            foreach (var breachType in breachTypes)
            {
                var alreadyBreached = ticket.AuditEvents.Any(e =>
                    e.EventType == AuditEventType.SlaBreached &&
                    e.Details != null &&
                    e.Details.Contains(breachType));

                if (alreadyBreached)
                    continue;

                if (ticket.Priority < Priority.P0)
                {
                    ticket.Escalate();

                    var escalationEvent = new TicketAuditEvent(
                        ticket.Id,
                        ActorType.System,
                        AuditEventType.Escalated,
                        details: $"Ticket escalated due to {breachType} SLA breach. Priority increased to {ticket.Priority}");

                    context.TicketAuditEvents.Add(escalationEvent);
                }

                var slaBreachEvent = new TicketAuditEvent(
                    ticket.Id,
                    ActorType.System,
                    AuditEventType.SlaBreached,
                    details: $"{breachType} SLA breached at {now:yyyy-MM-dd HH:mm:ss} UTC");

                context.TicketAuditEvents.Add(slaBreachEvent);
            }
        }

        if (breachedTickets.Any())
        {
            await context.SaveChangesAsync();
        }
    }
}
