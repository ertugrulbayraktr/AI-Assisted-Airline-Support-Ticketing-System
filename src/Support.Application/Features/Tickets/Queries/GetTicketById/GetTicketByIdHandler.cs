using Microsoft.EntityFrameworkCore;
using Support.Application.Common;
using Support.Application.Interfaces;
using Support.Domain.Enums;

namespace Support.Application.Features.Tickets.Queries.GetTicketById;

public class GetTicketByIdHandler
{
    private readonly IApplicationDbContext _context;

    public GetTicketByIdHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<TicketDetailDto>> Handle(GetTicketByIdQuery request, CancellationToken cancellationToken)
    {
        var ticket = await _context.Tickets
            .Include(t => t.Messages)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (ticket == null)
        {
            return Result<TicketDetailDto>.Failure("Ticket not found");
        }

        // Check access (passenger can only view own tickets)
        var requestingUser = await _context.Users.FindAsync(new object[] { request.RequestingUserId }, cancellationToken);
        
        var isPassenger = requestingUser == null || requestingUser.Role == Role.Passenger;
        if (isPassenger && ticket.CreatedByUserId != request.RequestingUserId)
        {
            return Result<TicketDetailDto>.Failure("Access denied");
        }

        // Load message authors
        var authorIds = ticket.Messages.Select(m => m.AuthorUserId).Distinct().ToList();
        var authors = await _context.Users
            .Where(u => authorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        var messages = ticket.Messages
            .Where(m => !isPassenger || !m.IsInternal)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new TicketMessageDto
            {
                Id = m.Id,
                AuthorUserId = m.AuthorUserId,
                AuthorName = authors.GetValueOrDefault(m.AuthorUserId, "Unknown"),
                Content = m.Content,
                IsInternal = m.IsInternal,
                CreatedAt = m.CreatedAt
            })
            .ToList();

        var result = new TicketDetailDto
        {
            Id = ticket.Id,
            TicketNumber = ticket.TicketNumber,
            State = ticket.State,
            Priority = ticket.Priority,
            Category = ticket.Category,
            Subject = ticket.Subject,
            Description = ticket.Description,
            PNR = ticket.PNR,
            CreatedByUserId = ticket.CreatedByUserId,
            AssignedToAgentId = ticket.AssignedToAgentId,
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt,
            FirstResponseDueAt = ticket.FirstResponseDueAt,
            ResolutionDueAt = ticket.ResolutionDueAt,
            FirstResponseAt = ticket.FirstResponseAt,
            ResolvedAt = ticket.ResolvedAt,
            ClosedAt = ticket.ClosedAt,
            SlaRisk = ticket.SlaRisk,
            Messages = messages
        };

        return Result<TicketDetailDto>.Success(result);
    }
}
