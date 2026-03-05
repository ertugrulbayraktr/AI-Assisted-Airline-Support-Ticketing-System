using System.Text.Json;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Support.Application.Interfaces;
using Support.Application.Models;
using Support.Domain.Enums;

namespace Support.Infrastructure.Services;

public class GeminiEmbeddingPolicySearchService : IPolicySearchService
{
    private readonly IApplicationDbContext _context;
    private readonly Client _client;
    private readonly string _embeddingModel;
    private readonly ILogger<GeminiEmbeddingPolicySearchService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GeminiEmbeddingPolicySearchService(
        IApplicationDbContext context,
        IConfiguration configuration,
        ILogger<GeminiEmbeddingPolicySearchService> logger)
    {
        _context = context;
        _logger = logger;

        var apiKey = configuration["Gemini:ApiKey"]
            ?? throw new InvalidOperationException("Gemini API key is not configured.");

        _embeddingModel = configuration["Gemini:EmbeddingModel"] ?? "text-embedding-004";
        _client = new Client(apiKey: apiKey);
    }

    public async Task<List<PolicyCitation>> SearchAsync(
        string query, int topK = 5, CancellationToken cancellationToken = default)
    {
        var chunks = await _context.PolicyChunks
            .Include(c => c.PolicyDocument)
            .Where(c => c.PolicyDocument.Status == PolicyStatus.Published)
            .ToListAsync(cancellationToken);

        var indexedChunks = chunks.Where(c => c.EmbeddingVector != null).ToList();

        if (!indexedChunks.Any())
        {
            _logger.LogWarning("No indexed policy chunks found, falling back to keyword search");
            return KeywordFallbackSearch(chunks, query, topK);
        }

        try
        {
            var queryEmbedding = await GenerateEmbeddingAsync(query, cancellationToken);

            var scored = indexedChunks.Select(chunk =>
            {
                var chunkVector = JsonSerializer.Deserialize<float[]>(chunk.EmbeddingVector!, JsonOptions)!;
                var similarity = CosineSimilarity(queryEmbedding, chunkVector);
                return new { Chunk = chunk, Score = similarity };
            })
            .Where(x => x.Score > 0.3)
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

            return scored;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Embedding search failed, falling back to keyword search");
            return KeywordFallbackSearch(chunks, query, topK);
        }
    }

    public async Task ReindexPolicyAsync(Guid policyDocumentId, CancellationToken cancellationToken = default)
    {
        var chunks = await _context.PolicyChunks
            .Where(c => c.PolicyDocumentId == policyDocumentId)
            .ToListAsync(cancellationToken);

        if (!chunks.Any())
        {
            _logger.LogWarning("No chunks found for policy {PolicyId}", policyDocumentId);
            return;
        }

        _logger.LogInformation("Generating embeddings for {Count} chunks of policy {PolicyId}",
            chunks.Count, policyDocumentId);

        foreach (var chunk in chunks)
        {
            try
            {
                var embedding = await GenerateEmbeddingAsync(chunk.Content, cancellationToken);
                var vectorJson = JsonSerializer.Serialize(embedding);
                chunk.SetEmbedding(vectorJson);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate embedding for chunk {ChunkId}", chunk.Id);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Embedding indexing completed for policy {PolicyId}", policyDocumentId);
    }

    private async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken)
    {
        var response = await _client.Models.EmbedContentAsync(
            model: _embeddingModel,
            contents: text,
            cancellationToken: cancellationToken);

        return response.Embeddings![0].Values!.Select(v => (float)v).ToArray();
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        double dot = 0, magA = 0, magB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        var magnitude = Math.Sqrt(magA) * Math.Sqrt(magB);
        return magnitude == 0 ? 0 : dot / magnitude;
    }

    private static List<PolicyCitation> KeywordFallbackSearch(
        List<Domain.Entities.PolicyChunk> chunks, string query, int topK)
    {
        var queryTerms = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return chunks.Select(chunk =>
        {
            var lower = chunk.Content.ToLower();
            var score = queryTerms.Sum(term => lower.Contains(term) ? 1.0 : 0.0);
            return new { Chunk = chunk, Score = score };
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
    }
}
