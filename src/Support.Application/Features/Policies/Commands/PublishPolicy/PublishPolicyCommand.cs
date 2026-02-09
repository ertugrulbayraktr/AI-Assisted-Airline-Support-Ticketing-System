namespace Support.Application.Features.Policies.Commands.PublishPolicy;

public class PublishPolicyCommand
{
    public Guid PolicyId { get; set; }
    public Guid PublishedByUserId { get; set; }
}
