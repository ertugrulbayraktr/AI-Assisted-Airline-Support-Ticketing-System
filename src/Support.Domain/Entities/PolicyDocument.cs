using Support.Domain.Common;
using Support.Domain.Enums;

namespace Support.Domain.Entities;

public class PolicyDocument : BaseEntity
{
    public string Title { get; private set; } = null!;
    public string Content { get; private set; } = null!; // Markdown
    public PolicyStatus Status { get; private set; }
    public int Version { get; private set; }
    
    public Guid AuthorId { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    
    // Navigation
    public List<PolicyChunk> Chunks { get; private set; } = new();

    private PolicyDocument() { } // EF Core

    public PolicyDocument(string title, string content, Guid authorId)
    {
        Title = title;
        Content = content;
        Status = PolicyStatus.Draft;
        Version = 1;
        AuthorId = authorId;
    }

    public void Publish()
    {
        if (Status != PolicyStatus.Draft)
        {
            throw new InvalidOperationException($"Only draft policies can be published. Current status: {Status}");
        }

        Status = PolicyStatus.Published;
        PublishedAt = DateTime.UtcNow;
        UpdateTimestamp();
    }

    public void Archive()
    {
        if (Status != PolicyStatus.Published)
        {
            throw new InvalidOperationException($"Only published policies can be archived. Current status: {Status}");
        }

        Status = PolicyStatus.Archived;
        UpdateTimestamp();
    }

    public void UpdateContent(string newContent)
    {
        if (Status != PolicyStatus.Draft)
        {
            throw new InvalidOperationException($"Only draft policies can be edited. Current status: {Status}");
        }

        Content = newContent;
        Version++;
        UpdateTimestamp();
    }
}
