namespace Support.Application.Models;

public class AgentReplyDraft
{
    public string DraftText { get; set; } = null!;
    public List<PolicyCitation> Citations { get; set; } = new();
    public List<string> MissingInfoQuestions { get; set; } = new();
    public List<string> NextActions { get; set; } = new();
    public List<string> RiskFlags { get; set; } = new();
}
