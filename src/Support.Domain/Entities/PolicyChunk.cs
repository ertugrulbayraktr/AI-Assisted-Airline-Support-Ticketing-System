using Support.Domain.Common;

namespace Support.Domain.Entities;

public class PolicyChunk : BaseEntity
{
    public Guid PolicyDocumentId { get; private set; }
    public string SectionTitle { get; private set; } = null!;
    public string Content { get; private set; } = null!;
    public int ChunkIndex { get; private set; }
    
    // For future embedding support
    public string? EmbeddingVector { get; private set; }
    
    // Navigation
    public PolicyDocument PolicyDocument { get; private set; } = null!;

    private PolicyChunk() { } // EF Core

    public PolicyChunk(Guid policyDocumentId, string sectionTitle, string content, int chunkIndex)
    {
        PolicyDocumentId = policyDocumentId;
        SectionTitle = sectionTitle;
        Content = content;
        ChunkIndex = chunkIndex;
    }

    public void SetEmbedding(string embeddingVector)
    {
        EmbeddingVector = embeddingVector;
        UpdateTimestamp();
    }
}
