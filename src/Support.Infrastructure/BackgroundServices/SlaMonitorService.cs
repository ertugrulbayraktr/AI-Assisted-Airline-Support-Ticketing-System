using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Support.Application.Interfaces;
using Support.Domain.Entities;
using Support.Domain.Enums;

namespace Support.Infrastructure.BackgroundServices;

public class SlaMonitorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SlaMonitorService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(2);

    public SlaMonitorService(IServiceProvider serviceProvider, ILogger<SlaMonitorService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SLA Monitor Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndEscalateTickets(stoppingToken);
                await AutoCloseResolvedTickets(stoppingToken);
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SLA Monitor Service");
            }
        }

        _logger.LogInformation("SLA Monitor Service stopped");
    }

    private async Task CheckAndEscalateTickets(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var now = DateTime.UtcNow;

        // Find tickets breaching SLA
        var breachedTickets = await context.Tickets
            .Include(t => t.AuditEvents) // IDEMPOTENCY: Load audit history
            .Where(t => 
                (t.FirstResponseAt == null && t.FirstResponseDueAt < now) ||
                (t.ResolvedAt == null && t.State != TicketState.Closed && t.State != TicketState.Cancelled && t.ResolutionDueAt < now))
            .ToListAsync(cancellationToken);

        var processedCount = 0;

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

                _logger.LogWarning("SLA breach detected for ticket {TicketNumber}: {BreachType}", 
                    ticket.TicketNumber, breachType);

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
                            $"Ticket {ticket.TicketNumber} has breached {breachType} SLA and has been escalated to {ticket.Priority}",
                            ticket.Id);

                        context.Notifications.Add(notification);
                    }

                    _logger.LogInformation("Ticket {TicketNumber} escalated to priority {NewPriority}",
                        ticket.TicketNumber, ticket.Priority);
                }

                var slaBreachEvent = new TicketAuditEvent(
                    ticket.Id,
                    ActorType.System,
                    AuditEventType.SlaBreached,
                    details: $"{breachType} SLA breached at {now:yyyy-MM-dd HH:mm:ss} UTC");

                context.TicketAuditEvents.Add(slaBreachEvent);
                processedCount++;
            }
        }

        if (processedCount > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Processed {Count} SLA breaches", processedCount);
        }
    }

    private async Task AutoCloseResolvedTickets(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var autoCloseThreshold = DateTime.UtcNow.AddHours(-72);

        var ticketsToClose = await context.Tickets
            .Where(t => t.State == TicketState.Resolved && t.ResolvedAt.HasValue && t.ResolvedAt < autoCloseThreshold)
            .ToListAsync(cancellationToken);

        foreach (var ticket in ticketsToClose)
        {
            ticket.Transition(TicketState.Closed);

            var auditEvent = new TicketAuditEvent(
                ticket.Id,
                ActorType.System,
                AuditEventType.TicketClosed,
                details: "Ticket auto-closed after 72 hours in Resolved state");

            context.TicketAuditEvents.Add(auditEvent);

            _logger.LogInformation("Ticket {TicketNumber} auto-closed", ticket.TicketNumber);
        }

        if (ticketsToClose.Any())
        {
            await context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Auto-closed {Count} resolved tickets", ticketsToClose.Count);
        }
    }
}
