using Support.Domain.Enums;

namespace Support.Application.Features.Tickets.Commands.CreateInternalTicket;

public class CreateInternalTicketCommand
{
    public string Subject { get; set; } = null!;
    public string Description { get; set; } = null!;
    public TicketCategory Category { get; set; }
    public Priority Priority { get; set; }
    public string? PNR { get; set; }
    public string? PassengerLastName { get; set; }
    
    // Set from JWT claims
    public Guid UserId { get; set; }
}

public class CreateInternalTicketResult
{
    public Guid TicketId { get; set; }
    public string TicketNumber { get; set; } = null!;
}
