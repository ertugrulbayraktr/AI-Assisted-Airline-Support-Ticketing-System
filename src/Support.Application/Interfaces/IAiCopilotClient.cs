using Support.Application.Models;

namespace Support.Application.Interfaces;

public interface IAiCopilotClient
{
    Task<TicketCreationDraft> DraftTicketCreateAsync(
        string passengerFreeText,
        ReservationInfo? reservationContext,
        CancellationToken cancellationToken = default);

    Task<AgentReplyDraft> DraftAgentReplyAsync(
        TicketThreadContext ticketContext,
        ReservationInfo? reservationContext,
        List<PolicyCitation> policyChunks,
        CancellationToken cancellationToken = default);
}
