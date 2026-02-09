namespace Support.Application.Features.Policies.Commands.CreatePolicy;

public class CreatePolicyCommand
{
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    public Guid AuthorId { get; set; }
}

public class CreatePolicyResult
{
    public Guid PolicyId { get; set; }
}
