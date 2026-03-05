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
    private readonly IAiCopilotClient _aiCopilot;

    public CreateTicketHandler(
        IApplicationDbContext context,
        IReservationProvider reservationProvider,
        IAiCopilotClient aiCopilot)
    {
        _context = context;
        _reservationProvider = reservationProvider;
        _aiCopilot = aiCopilot;
    }

    public async Task<Result<CreateTicketResult>> Handle(CreateTicketCommand request, CancellationToken cancellationToken)
    {
        var reservation = await _reservationProvider.GetReservationAsync(request.PNR, request.PassengerLastName, cancellationToken);
        if (reservation == null)
        {
            return Result<CreateTicketResult>.Failure("Invalid PNR or passenger last name");
        }

        var draft = await _aiCopilot.DraftTicketCreateAsync(
            $"{request.Subject} {request.Description}",
            reservation,
            cancellationToken);

        var ticket = new Ticket(
            request.Subject,
            request.Description,
            draft.CategorySuggested,
            draft.PrioritySuggested,
            request.UserId,
            request.PNR,
            request.PassengerLastName);

        _context.Tickets.Add(ticket);

        var auditEvent = new TicketAuditEvent(
            ticket.Id,
            ActorType.Passenger,
            AuditEventType.Created,
            request.UserId,
            details: $"Ticket created by passenger with PNR {request.PNR}. AI suggested category: {draft.CategorySuggested}, priority: {draft.PrioritySuggested}");

        _context.TicketAuditEvents.Add(auditEvent);

        await _context.SaveChangesAsync(cancellationToken);

        return Result<CreateTicketResult>.Success(new CreateTicketResult
        {
            TicketId = ticket.Id,
            TicketNumber = ticket.TicketNumber,
            AiSummary = draft.Summary,
            SuggestedCategory = draft.CategorySuggested,
            SuggestedPriority = draft.PrioritySuggested
        });
    }
}
