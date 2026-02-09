using Microsoft.EntityFrameworkCore;
using Support.Application.Common;
using Support.Application.Interfaces;
using Support.Domain.Entities;
using Support.Domain.Enums;
using System.Text.RegularExpressions;

namespace Support.Application.Features.Policies.Commands.PublishPolicy;

public class PublishPolicyHandler
{
    private readonly IApplicationDbContext _context;

    public PublishPolicyHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(PublishPolicyCommand request, CancellationToken cancellationToken)
    {
        var policy = await _context.PolicyDocuments
            .Include(p => p.Chunks)
            .FirstOrDefaultAsync(p => p.Id == request.PolicyId, cancellationToken);

        if (policy == null)
        {
            return Result.Failure("Policy not found");
        }

        policy.Publish();

        // Remove old chunks
        _context.PolicyChunks.RemoveRange(policy.Chunks);

        // Chunk the content by markdown headings
        var chunks = ChunkMarkdownContent(policy.Content, policy.Id);
        foreach (var chunk in chunks)
        {
            _context.PolicyChunks.Add(chunk);
        }

        var auditEvent = new TicketAuditEvent(
            Guid.Empty, // No specific ticket
            ActorType.Admin,
            AuditEventType.PolicyPublished,
            request.PublishedByUserId,
            details: $"Policy '{policy.Title}' published with {chunks.Count} chunks");

        _context.TicketAuditEvents.Add(auditEvent);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private List<PolicyChunk> ChunkMarkdownContent(string markdown, Guid policyId)
    {
        var chunks = new List<PolicyChunk>();
        var lines = markdown.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        string currentSection = "Introduction";
        var currentContent = new List<string>();
        int chunkIndex = 0;

        foreach (var line in lines)
        {
            // Check if it's a heading (## or ###)
            var headingMatch = Regex.Match(line, @"^#+\s+(.+)$");
            if (headingMatch.Success)
            {
                // Save previous section
                if (currentContent.Any())
                {
                    chunks.Add(new PolicyChunk(
                        policyId,
                        currentSection,
                        string.Join("\n", currentContent),
                        chunkIndex++));
                    currentContent.Clear();
                }

                currentSection = headingMatch.Groups[1].Value;
            }
            else
            {
                currentContent.Add(line);
            }
        }

        // Save last section
        if (currentContent.Any())
        {
            chunks.Add(new PolicyChunk(
                policyId,
                currentSection,
                string.Join("\n", currentContent),
                chunkIndex));
        }

        return chunks;
    }
}
