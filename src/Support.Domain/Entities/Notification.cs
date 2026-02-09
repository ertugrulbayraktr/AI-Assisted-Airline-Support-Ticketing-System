using Support.Domain.Common;

namespace Support.Domain.Entities;

public class Notification : BaseEntity
{
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = null!;
    public string Message { get; private set; } = null!;
    public bool IsRead { get; private set; }
    
    // Optional link to ticket
    public Guid? TicketId { get; private set; }

    private Notification() { } // EF Core

    public Notification(Guid userId, string title, string message, Guid? ticketId = null)
    {
        UserId = userId;
        Title = title;
        Message = message;
        IsRead = false;
        TicketId = ticketId;
    }

    public void MarkAsRead()
    {
        IsRead = true;
        UpdateTimestamp();
    }
}
