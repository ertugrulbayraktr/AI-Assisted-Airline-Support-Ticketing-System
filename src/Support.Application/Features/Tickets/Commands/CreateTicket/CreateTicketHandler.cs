using Microsoft.EntityFrameworkCore;
using Support.Application.Common;
using Support.Application.Interfaces;
using Support.Domain.Entities;
using Support.Domain.Enums;

namespace Support.Application.Features.Tickets.Commands.CreateTicket;

public class CreateTicketHandler
{
    private readonly IApplicationDbContext _context;
    private readonly IReservationProvider _reservationProvider;

    public CreateTicketHandler(IApplicationDbContext context, IReservationProvider reservationProvider)
    {
        _context = context;
        _reservationProvider = reservationProvider;
    }

    public async Task<Result<CreateTicketResult>> Handle(CreateTicketCommand request, CancellationToken cancellationToken)
    {
        // Verify PNR
        var isValid = await _reservationProvider.VerifyPnrAsync(request.PNR, request.PassengerLastName, cancellationToken);
        if (!isValid)
        {
            return Result<CreateTicketResult>.Failure("Invalid PNR or passenger last name");
        }

        // Create ticket
        var ticket = new Ticket(
            request.Subject,
            request.Description,
            request.Category,
            request.Priority,
            request.UserId,
            request.PNR,
            request.PassengerLastName);

        _context.Tickets.Add(ticket);

        // Add audit event
        var auditEvent = new TicketAuditEvent(
            ticket.Id,
            ActorType.Passenger,
            AuditEventType.Created,
            request.UserId,
            details: $"Ticket created by passenger with PNR {request.PNR}");

        _context.TicketAuditEvents.Add(auditEvent);

        await _context.SaveChangesAsync(cancellationToken);

        return Result<CreateTicketResult>.Success(new CreateTicketResult
        {
            TicketId = ticket.Id,
            TicketNumber = ticket.TicketNumber
        });
    }
}
