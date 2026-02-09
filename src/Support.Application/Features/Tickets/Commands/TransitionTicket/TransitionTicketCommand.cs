using Support.Domain.Enums;

namespace Support.Application.Features.Tickets.Commands.TransitionTicket;

public class TransitionTicketCommand
{
    public Guid TicketId { get; set; }
    public TicketState NewState { get; set; }
    
    // Set from JWT claims
    public Guid UserId { get; set; }
}
