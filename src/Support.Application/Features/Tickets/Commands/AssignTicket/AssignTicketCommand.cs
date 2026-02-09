namespace Support.Application.Features.Tickets.Commands.AssignTicket;

public class AssignTicketCommand
{
    public Guid TicketId { get; set; }
    public Guid AgentId { get; set; }
    
    // Set from JWT claims
    public Guid AssignedByUserId { get; set; }
}
