using Microsoft.EntityFrameworkCore;
using Support.Application.Common;
using Support.Application.Interfaces;

namespace Support.Application.Features.Policies.Queries.GetPolicyById;

public class GetPolicyByIdHandler
{
    private readonly IApplicationDbContext _context;

    public GetPolicyByIdHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PolicyDetailDto>> Handle(GetPolicyByIdQuery request, CancellationToken cancellationToken)
    {
        var policy = await _context.PolicyDocuments
            .AsNoTracking()
            .Include(p => p.Chunks)
            .FirstOrDefaultAsync(p => p.Id == request.PolicyId, cancellationToken);

        if (policy == null)
        {
            return Result<PolicyDetailDto>.Failure("Policy not found");
        }

        return Result<PolicyDetailDto>.Success(new PolicyDetailDto
        {
            Id = policy.Id,
            Title = policy.Title,
            Content = policy.Content,
            Status = policy.Status,
            Version = policy.Version,
            AuthorId = policy.AuthorId,
            PublishedAt = policy.PublishedAt,
            CreatedAt = policy.CreatedAt,
            UpdatedAt = policy.UpdatedAt,
            ChunkCount = policy.Chunks.Count
        });
    }
}
