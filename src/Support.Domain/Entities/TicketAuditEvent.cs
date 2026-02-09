using Support.Domain.Common;
using Support.Domain.Enums;

namespace Support.Domain.Entities;

public class TicketAuditEvent : BaseEntity
{
    public Guid TicketId { get; private set; }
    public DateTime Timestamp { get; private set; }
    public ActorType ActorType { get; private set; }
    public Guid? ActorId { get; private set; }
    public AuditEventType EventType { get; private set; }
    
    public string? BeforeState { get; private set; }
    public string? AfterState { get; private set; }
    public string? Details { get; private set; }
    
    // Navigation
    public Ticket Ticket { get; private set; } = null!;

    private TicketAuditEvent() { } // EF Core

    public TicketAuditEvent(
        Guid ticketId,
        ActorType actorType,
        AuditEventType eventType,
        Guid? actorId = null,
        string? beforeState = null,
        string? afterState = null,
        string? details = null)
    {
        TicketId = ticketId;
        Timestamp = DateTime.UtcNow;
        ActorType = actorType;
        ActorId = actorId;
        EventType = eventType;
        BeforeState = beforeState;
        AfterState = afterState;
        Details = details;
    }
}
