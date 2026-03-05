using Support.Domain.Enums;

namespace Support.Application.Features.Tickets.Commands.CreateTicket;

public class CreateTicketCommand
{
    public string Subject { get; set; } = null!;
    public string Description { get; set; } = null!;
    public TicketCategory Category { get; set; }
    public Priority Priority { get; set; }
    public string PNR { get; set; } = null!;
    public string PassengerLastName { get; set; } = null!;
    
    // Set from JWT claims
    public Guid UserId { get; set; }
}

public class CreateTicketResult
{
    public Guid TicketId { get; set; }
    public string TicketNumber { get; set; } = null!;
    public string? AiSummary { get; set; }
    public Domain.Enums.TicketCategory? SuggestedCategory { get; set; }
    public Domain.Enums.Priority? SuggestedPriority { get; set; }
}
