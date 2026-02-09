using Support.Domain.Enums;

namespace Support.Application.Features.Tickets.Queries.GetAgentQueue;

public class GetAgentQueueQuery
{
    public TicketState? FilterByState { get; set; }
    public Priority? FilterByPriority { get; set; }
    public TicketCategory? FilterByCategory { get; set; }
    public bool? FilterBySlaRisk { get; set; }
    public Guid? FilterByAssignedToMe { get; set; }
    public string? SortBy { get; set; } = "CreatedAt"; // CreatedAt, Priority, UpdatedAt
    public bool SortDescending { get; set; } = true;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class GetAgentQueueResult
{
    public List<AgentTicketDto> Tickets { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}

public class AgentTicketDto
{
    public Guid Id { get; set; }
    public string TicketNumber { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public TicketState State { get; set; }
    public Priority Priority { get; set; }
    public TicketCategory Category { get; set; }
    public string? PNR { get; set; }
    public Guid? AssignedToAgentId { get; set; }
    public string? AssignedToAgentName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime FirstResponseDueAt { get; set; }
    public DateTime ResolutionDueAt { get; set; }
    public bool SlaRisk { get; set; }
}
