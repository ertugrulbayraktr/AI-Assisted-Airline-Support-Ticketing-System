using Microsoft.EntityFrameworkCore;
using Support.Application.Common;
using Support.Application.Interfaces;
using Support.Domain.Entities;
using Support.Domain.Enums;

namespace Support.Application.Features.Tickets.Commands.TransitionTicket;

public class TransitionTicketHandler
{
    private readonly IApplicationDbContext _context;

    public TransitionTicketHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(TransitionTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await _context.Tickets
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (ticket == null)
        {
            return Result.Failure("Ticket not found");
        }

        var oldState = ticket.State;

        try
        {
            ticket.Transition(request.NewState);

            var auditEvent = new TicketAuditEvent(
                ticket.Id,
                ActorType.Agent,
                AuditEventType.StateChanged,
                request.UserId,
                beforeState: oldState.ToString(),
                afterState: request.NewState.ToString(),
                details: $"State changed from {oldState} to {request.NewState}");

            _context.TicketAuditEvents.Add(auditEvent);

            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message);
        }
    }
}
