using Support.Domain.Enums;

namespace Support.Application.Models;

public class TicketThreadContext
{
    public Guid TicketId { get; set; }
    public string TicketNumber { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public TicketCategory Category { get; set; }
    public Priority Priority { get; set; }
    public TicketState State { get; set; }
    public string? PNR { get; set; }
    public List<MessageInfo> Messages { get; set; } = new();
}

public class MessageInfo
{
    public string AuthorName { get; set; } = null!;
    public string Content { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public bool IsInternal { get; set; }
}
