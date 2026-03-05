using Microsoft.EntityFrameworkCore;
using Support.Application.Common;
using Support.Application.Interfaces;
using Support.Domain.Entities;
using Support.Domain.Enums;

namespace Support.Application.Features.Tickets.Commands.AddMessage;

public class AddMessageHandler
{
    private readonly IApplicationDbContext _context;

    public AddMessageHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<AddMessageResult>> Handle(AddMessageCommand request, CancellationToken cancellationToken)
    {
        var ticket = await _context.Tickets
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (ticket == null)
        {
            return Result<AddMessageResult>.Failure("Ticket not found");
        }

        if (ticket.State == TicketState.Closed || ticket.State == TicketState.Cancelled)
        {
            return Result<AddMessageResult>.Failure($"Cannot add messages to a {ticket.State} ticket");
        }

        // SECURITY: Verify user identity and enforce internal note permissions
        var user = await _context.Users.FindAsync(new object[] { request.UserId }, cancellationToken);
        
        if (user == null)
        {
            return Result<AddMessageResult>.Failure("User not found");
        }

        // CRITICAL SECURITY: Passengers CANNOT create internal notes
        // Force isInternal to false if user is a passenger
        var canCreateInternalNote = user.Role == Role.SupportAgent || user.Role == Role.Admin;
        var actualIsInternal = request.IsInternal && canCreateInternalNote;

        var message = new TicketMessage(request.TicketId, request.UserId, request.Content, actualIsInternal);
        _context.TicketMessages.Add(message);

        // Record first response if agent message
        if (user.Role == Role.SupportAgent && !ticket.FirstResponseAt.HasValue)
        {
            ticket.RecordFirstResponse();
        }

        var eventType = actualIsInternal ? AuditEventType.InternalNoteAdded : AuditEventType.MessageAdded;
        var actorType = user.Role == Role.Passenger ? ActorType.Passenger : ActorType.Agent;

        var auditEvent = new TicketAuditEvent(
            ticket.Id,
            actorType,
            eventType,
            request.UserId,
            details: request.Content.Length > 50
                ? $"Message added: {request.Content[..50]}..."
                : $"Message added: {request.Content}");

        _context.TicketAuditEvents.Add(auditEvent);

        await _context.SaveChangesAsync(cancellationToken);

        return Result<AddMessageResult>.Success(new AddMessageResult { MessageId = message.Id });
    }
}
