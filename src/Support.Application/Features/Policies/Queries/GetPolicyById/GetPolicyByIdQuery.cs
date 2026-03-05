using Support.Domain.Enums;

namespace Support.Application.Features.Policies.Queries.GetPolicyById;

public class GetPolicyByIdQuery
{
    public Guid PolicyId { get; set; }
}

public class PolicyDetailDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    public PolicyStatus Status { get; set; }
    public int Version { get; set; }
    public Guid AuthorId { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int ChunkCount { get; set; }
}
