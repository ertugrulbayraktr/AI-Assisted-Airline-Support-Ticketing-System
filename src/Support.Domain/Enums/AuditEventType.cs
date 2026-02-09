namespace Support.Domain.Enums;

public enum AuditEventType
{
    Created = 0,
    MessageAdded = 1,
    InternalNoteAdded = 2,
    Assigned = 3,
    StateChanged = 4,
    PriorityChanged = 5,
    SlaBreached = 6,
    Escalated = 7,
    PolicyPublished = 8,
    PolicyReindexed = 9,
    TicketClosed = 10,
    TicketCancelled = 11
}
