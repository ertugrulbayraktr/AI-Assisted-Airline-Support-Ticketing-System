using Support.Application.Interfaces;
using Support.Application.Models;
using Support.Domain.Enums;

namespace Support.Infrastructure.Services;

public class MockAiCopilotClient : IAiCopilotClient
{
    public Task<TicketCreationDraft> DraftTicketCreateAsync(
        string passengerFreeText,
        ReservationInfo? reservationContext,
        CancellationToken cancellationToken = default)
    {
        // Simple keyword-based mock analysis
        var lowerText = passengerFreeText.ToLower();

        var category = DetermineCategory(lowerText);
        var priority = DeterminePriority(lowerText, reservationContext);

        var draft = new TicketCreationDraft
        {
            Summary = ExtractSummary(passengerFreeText),
            CategorySuggested = category,
            PrioritySuggested = priority,
            ClarifyingQuestions = new List<string>
            {
                "Could you provide your booking reference number?",
                "What is your preferred contact method?"
            }
        };

        return Task.FromResult(draft);
    }

    public Task<AgentReplyDraft> DraftAgentReplyAsync(
        TicketThreadContext ticketContext,
        ReservationInfo? reservationContext,
        List<PolicyCitation> policyChunks,
        CancellationToken cancellationToken = default)
    {
        var draftText = GenerateDraftReply(ticketContext, reservationContext, policyChunks);

        var draft = new AgentReplyDraft
        {
            DraftText = draftText,
            Citations = policyChunks.Take(3).ToList(),
            MissingInfoQuestions = new List<string>
            {
                "Have you tried the suggested resolution?",
                "Is there anything else we can help with?"
            },
            NextActions = new List<string>
            {
                "Follow up in 24 hours if not resolved",
                "Escalate to supervisor if customer requests"
            },
            RiskFlags = DetectRiskFlags(ticketContext, reservationContext)
        };

        return Task.FromResult(draft);
    }

    private TicketCategory DetermineCategory(string text)
    {
        if (text.Contains("cancel")) return TicketCategory.Cancellation;
        if (text.Contains("refund")) return TicketCategory.Refund;
        if (text.Contains("baggage") || text.Contains("luggage")) return TicketCategory.Baggage;
        if (text.Contains("delay")) return TicketCategory.FlightDelay;
        if (text.Contains("seat")) return TicketCategory.SeatChange;
        if (text.Contains("meal")) return TicketCategory.MealRequest;
        return TicketCategory.General;
    }

    private Priority DeterminePriority(string text, ReservationInfo? reservation)
    {
        var lowerText = text.ToLower();

        // Critical keywords
        if (lowerText.Contains("urgent") || lowerText.Contains("emergency") ||
            lowerText.Contains("stranded") || lowerText.Contains("medical"))
            return Priority.P0;

        // Flight status matters
        if (reservation?.FlightStatus == "cancelled")
            return Priority.P1;

        if (lowerText.Contains("asap") || lowerText.Contains("immediately"))
            return Priority.P1;

        if (reservation?.FlightStatus == "delayed")
            return Priority.P2;

        return Priority.P3;
    }

    private string ExtractSummary(string text)
    {
        var firstLine = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return firstLine?.Length > 100 ? firstLine[..97] + "..." : firstLine ?? text;
    }

    private string GenerateDraftReply(TicketThreadContext context, ReservationInfo? reservation, List<PolicyCitation> policies)
    {
        var greeting = $"Dear Customer,\n\nThank you for contacting us regarding your inquiry about {context.Category}.\n\n";

        var body = context.Category switch
        {
            TicketCategory.Cancellation => "I understand you wish to cancel your booking. According to our policy, " +
                                          (reservation?.FareRules.IsRefundable == true
                                              ? "your ticket is refundable. "
                                              : "this ticket is non-refundable, but we can offer a credit for future travel. "),
            TicketCategory.Refund => "I've reviewed your refund request. Based on your fare type, " +
                                     (reservation?.FareRules.IsRefundable == true
                                         ? $"you are eligible for a full refund minus a ${reservation.FareRules.CancellationFee} processing fee. "
                                         : "this ticket is non-refundable according to the fare rules. "),
            TicketCategory.FlightDelay => "I apologize for the inconvenience caused by the flight delay. " +
                                          "We are working to get you to your destination as soon as possible. ",
            _ => "I'm here to assist you with your inquiry. "
        };

        var policyReference = policies.Any()
            ? $"\n\nThis is in accordance with our {string.Join(", ", policies.Take(2).Select(p => p.SectionTitle))} policy."
            : "";

        var closing = "\n\nPlease let me know if you need any further assistance.\n\nBest regards,\nCustomer Support Team";

        return greeting + body + policyReference + closing;
    }

    private List<string> DetectRiskFlags(TicketThreadContext context, ReservationInfo? reservation)
    {
        var flags = new List<string>();

        if (context.Priority >= Priority.P1)
            flags.Add("High priority ticket");

        if (reservation?.FlightStatus == "cancelled")
            flags.Add("Flight cancelled - customer may need rebooking");

        if (context.State == TicketState.WaitingOnPassenger)
        {
            var lastMessage = context.Messages.LastOrDefault();
            if (lastMessage != null && (DateTime.UtcNow - lastMessage.CreatedAt).TotalHours > 48)
            {
                flags.Add("No passenger response in 48+ hours");
            }
        }

        return flags;
    }
}
