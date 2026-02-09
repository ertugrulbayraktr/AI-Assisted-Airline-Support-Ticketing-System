using Microsoft.EntityFrameworkCore;
using Support.Application.Common;
using Support.Application.Interfaces;

namespace Support.Application.Features.Tickets.Queries.GetMyTickets;

public class GetMyTicketsHandler
{
    private readonly IApplicationDbContext _context;

    public GetMyTicketsHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<GetMyTicketsResult>> Handle(GetMyTicketsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Tickets
            .Where(t => t.CreatedByUserId == request.UserId);

        if (request.FilterByState.HasValue)
        {
            query = query.Where(t => t.State == request.FilterByState.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var tickets = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new TicketSummaryDto
            {
                Id = t.Id,
                TicketNumber = t.TicketNumber,
                Subject = t.Subject,
                State = t.State,
                Priority = t.Priority,
                Category = t.Category,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                SlaRisk = t.SlaRisk
            })
            .ToListAsync(cancellationToken);

        return Result<GetMyTicketsResult>.Success(new GetMyTicketsResult
        {
            Tickets = tickets,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        });
    }
}
