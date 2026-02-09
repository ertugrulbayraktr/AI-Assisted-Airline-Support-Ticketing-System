using Support.Domain.Common;

namespace Support.Domain.Entities;

public class TicketMessage : BaseEntity
{
    public Guid TicketId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public string Content { get; private set; } = null!;
    public bool IsInternal { get; private set; }
    
    // Navigation
    public Ticket Ticket { get; private set; } = null!;

    private TicketMessage() { } // EF Core

    public TicketMessage(Guid ticketId, Guid authorUserId, string content, bool isInternal = false)
    {
        TicketId = ticketId;
        AuthorUserId = authorUserId;
        Content = content;
        IsInternal = isInternal;
    }
}
