using Microsoft.EntityFrameworkCore;
using Support.Application.Interfaces;
using Support.Application.Models;
using Support.Domain.Enums;

namespace Support.Infrastructure.Services;

public class PolicySearchService : IPolicySearchService
{
    private readonly IApplicationDbContext _context;

    public PolicySearchService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<PolicyCitation>> SearchAsync(string query, int topK = 5, CancellationToken cancellationToken = default)
    {
        // Simple keyword-based search (MVP)
        var queryTerms = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var chunks = await _context.PolicyChunks
            .Include(c => c.PolicyDocument)
            .Where(c => c.PolicyDocument.Status == PolicyStatus.Published)
            .ToListAsync(cancellationToken);

        var scoredChunks = chunks.Select(chunk =>
        {
            var score = CalculateTfIdfScore(chunk.Content.ToLower(), queryTerms);
            return new
            {
                Chunk = chunk,
                Score = score
            };
        })
        .Where(x => x.Score > 0)
        .OrderByDescending(x => x.Score)
        .Take(topK)
        .Select(x => new PolicyCitation
        {
            PolicyId = x.Chunk.PolicyDocumentId,
            SectionTitle = x.Chunk.SectionTitle,
            Content = x.Chunk.Content,
            Score = x.Score
        })
        .ToList();

        return scoredChunks;
    }

    public async Task ReindexPolicyAsync(Guid policyDocumentId, CancellationToken cancellationToken = default)
    {
        // Placeholder for future embedding-based reindexing
        // For now, chunks are already indexed during publish
        await Task.CompletedTask;
    }

    private double CalculateTfIdfScore(string content, string[] queryTerms)
    {
        var score = 0.0;

        foreach (var term in queryTerms)
        {
            if (content.Contains(term))
            {
                // Simple term frequency
                var count = CountOccurrences(content, term);
                score += count * 1.0; // Basic scoring
            }
        }

        return score;
    }

    private int CountOccurrences(string text, string term)
    {
        var count = 0;
        var index = 0;

        while ((index = text.IndexOf(term, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += term.Length;
        }

        return count;
    }
}
