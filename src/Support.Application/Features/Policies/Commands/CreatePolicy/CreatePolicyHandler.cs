using Support.Application.Common;
using Support.Application.Interfaces;
using Support.Domain.Entities;

namespace Support.Application.Features.Policies.Commands.CreatePolicy;

public class CreatePolicyHandler
{
    private readonly IApplicationDbContext _context;

    public CreatePolicyHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<CreatePolicyResult>> Handle(CreatePolicyCommand request, CancellationToken cancellationToken)
    {
        var policy = new PolicyDocument(request.Title, request.Content, request.AuthorId);

        _context.PolicyDocuments.Add(policy);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<CreatePolicyResult>.Success(new CreatePolicyResult { PolicyId = policy.Id });
    }
}
