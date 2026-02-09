namespace Support.Application.Features.Tickets.Commands.AddMessage;

public class AddMessageCommand
{
    public Guid TicketId { get; set; }
    public string Content { get; set; } = null!;
    public bool IsInternal { get; set; }
    
    // Set from JWT claims
    public Guid UserId { get; set; }
}

public class AddMessageResult
{
    public Guid MessageId { get; set; }
}
