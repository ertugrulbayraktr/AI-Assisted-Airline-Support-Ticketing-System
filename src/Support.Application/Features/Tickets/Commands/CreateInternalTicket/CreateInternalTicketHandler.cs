using Support.Application.Common;
using Support.Application.Interfaces;
using Support.Domain.Entities;
using Support.Domain.Enums;

namespace Support.Application.Features.Tickets.Commands.CreateInternalTicket;

public class CreateInternalTicketHandler
{
    private readonly IApplicationDbContext _context;

    public CreateInternalTicketHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<CreateInternalTicketResult>> Handle(CreateInternalTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = new Ticket(
            request.Subject,
            request.Description,
            request.Category,
            request.Priority,
            request.UserId,
            request.PNR,
            request.PassengerLastName);

        _context.Tickets.Add(ticket);

        var auditEvent = new TicketAuditEvent(
            ticket.Id,
            ActorType.Agent,
            AuditEventType.Created,
            request.UserId,
            details: "Internal ticket created by agent");

        _context.TicketAuditEvents.Add(auditEvent);

        await _context.SaveChangesAsync(cancellationToken);

        return Result<CreateInternalTicketResult>.Success(new CreateInternalTicketResult
        {
            TicketId = ticket.Id,
            TicketNumber = ticket.TicketNumber
        });
    }
}
