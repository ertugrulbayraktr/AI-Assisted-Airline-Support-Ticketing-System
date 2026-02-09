using Support.Domain.Enums;

namespace Support.Application.Features.Tickets.Queries.GetMyTickets;

public class GetMyTicketsQuery
{
    public Guid UserId { get; set; }
    public TicketState? FilterByState { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class GetMyTicketsResult
{
    public List<TicketSummaryDto> Tickets { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}

public class TicketSummaryDto
{
    public Guid Id { get; set; }
    public string TicketNumber { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public TicketState State { get; set; }
    public Priority Priority { get; set; }
    public TicketCategory Category { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool SlaRisk { get; set; }
}
