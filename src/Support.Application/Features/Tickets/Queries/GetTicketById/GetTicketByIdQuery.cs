using Support.Domain.Enums;

namespace Support.Application.Features.Tickets.Queries.GetTicketById;

public class GetTicketByIdQuery
{
    public Guid TicketId { get; set; }
    public Guid RequestingUserId { get; set; }
}

public class TicketDetailDto
{
    public Guid Id { get; set; }
    public string TicketNumber { get; set; } = null!;
    public TicketState State { get; set; }
    public Priority Priority { get; set; }
    public TicketCategory Category { get; set; }
    public string Subject { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string? PNR { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? AssignedToAgentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime FirstResponseDueAt { get; set; }
    public DateTime ResolutionDueAt { get; set; }
    public DateTime? FirstResponseAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public bool SlaRisk { get; set; }
    public List<TicketMessageDto> Messages { get; set; } = new();
}

public class TicketMessageDto
{
    public Guid Id { get; set; }
    public Guid AuthorUserId { get; set; }
    public string AuthorName { get; set; } = null!;
    public string Content { get; set; } = null!;
    public bool IsInternal { get; set; }
    public DateTime CreatedAt { get; set; }
}
