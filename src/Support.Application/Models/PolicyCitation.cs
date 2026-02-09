namespace Support.Application.Models;

public class PolicyCitation
{
    public Guid PolicyId { get; set; }
    public string SectionTitle { get; set; } = null!;
    public string Content { get; set; } = null!;
    public double Score { get; set; }
}
