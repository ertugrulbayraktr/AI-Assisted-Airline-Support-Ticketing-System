using Microsoft.EntityFrameworkCore;
using Support.Application.Common;
using Support.Application.Interfaces;

namespace Support.Application.Features.Tickets.Queries.GetAgentQueue;

public class GetAgentQueueHandler
{
    private readonly IApplicationDbContext _context;

    public GetAgentQueueHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<GetAgentQueueResult>> Handle(GetAgentQueueQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Tickets.AsQueryable();

        if (request.FilterByState.HasValue)
            query = query.Where(t => t.State == request.FilterByState.Value);

        if (request.FilterByPriority.HasValue)
            query = query.Where(t => t.Priority == request.FilterByPriority.Value);

        if (request.FilterByCategory.HasValue)
            query = query.Where(t => t.Category == request.FilterByCategory.Value);

        if (request.FilterBySlaRisk.HasValue)
            query = query.Where(t => t.SlaRisk == request.FilterBySlaRisk.Value);

        if (request.FilterByAssignedToMe.HasValue)
            query = query.Where(t => t.AssignedToAgentId == request.FilterByAssignedToMe.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        // Sorting
        query = request.SortBy?.ToLower() switch
        {
            "priority" => request.SortDescending
                ? query.OrderByDescending(t => t.Priority)
                : query.OrderBy(t => t.Priority),
            "updatedat" => request.SortDescending
                ? query.OrderByDescending(t => t.UpdatedAt)
                : query.OrderBy(t => t.UpdatedAt),
            _ => request.SortDescending
                ? query.OrderByDescending(t => t.CreatedAt)
                : query.OrderBy(t => t.CreatedAt)
        };

        var tickets = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new AgentTicketDto
            {
                Id = t.Id,
                TicketNumber = t.TicketNumber,
                Subject = t.Subject,
                State = t.State,
                Priority = t.Priority,
                Category = t.Category,
                PNR = t.PNR,
                AssignedToAgentId = t.AssignedToAgentId,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                FirstResponseDueAt = t.FirstResponseDueAt,
                ResolutionDueAt = t.ResolutionDueAt,
                SlaRisk = t.SlaRisk
            })
            .ToListAsync(cancellationToken);

        // Load agent names
        var agentIds = tickets.Where(t => t.AssignedToAgentId.HasValue)
            .Select(t => t.AssignedToAgentId!.Value)
            .Distinct()
            .ToList();

        var agents = await _context.Users
            .Where(u => agentIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        foreach (var ticket in tickets.Where(t => t.AssignedToAgentId.HasValue))
        {
            ticket.AssignedToAgentName = agents.GetValueOrDefault(ticket.AssignedToAgentId!.Value);
        }

        return Result<GetAgentQueueResult>.Success(new GetAgentQueueResult
        {
            Tickets = tickets,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        });
    }
}
