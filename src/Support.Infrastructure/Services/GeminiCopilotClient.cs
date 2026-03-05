using System.Text.Json;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Support.Application.Interfaces;
using Support.Application.Models;
using Support.Domain.Enums;

namespace Support.Infrastructure.Services;

public class GeminiCopilotClient : IAiCopilotClient
{
    private readonly Client _client;
    private readonly string _model;
    private readonly ILogger<GeminiCopilotClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GeminiCopilotClient(IConfiguration configuration, ILogger<GeminiCopilotClient> logger)
    {
        var apiKey = configuration["Gemini:ApiKey"]
            ?? throw new InvalidOperationException("Gemini API key is not configured. Set 'Gemini:ApiKey' via user-secrets.");

        _model = configuration["Gemini:Model"] ?? "gemini-2.0-flash";
        _client = new Client(apiKey: apiKey);
        _logger = logger;
    }

    public async Task<TicketCreationDraft> DraftTicketCreateAsync(
        string passengerFreeText,
        ReservationInfo? reservationContext,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = """
            You are an expert airline customer support ticket classifier for AirlineSupport.
            Your job is to analyze a passenger's message and accurately classify it.

            CLASSIFICATION RULES:

            Category (pick the MOST specific match):
            - Booking: new booking, modification to existing booking, upgrade request
            - Cancellation: passenger wants to cancel their flight
            - Refund: passenger wants money back (may overlap with cancellation — pick Refund if money is the focus)
            - Baggage: lost, delayed, damaged baggage, extra baggage request
            - FlightDelay: flight is delayed, passenger affected by delay
            - FlightCancellation: airline cancelled the flight (not passenger-initiated)
            - SeatChange: seat assignment, upgrade, accessibility seating
            - SpecialAssistance: wheelchair, unaccompanied minor, medical equipment, disability
            - MealRequest: dietary requirements, special meals
            - General: does not fit any above
            - Other: fits multiple categories equally or is ambiguous

            Priority (consider ALL factors together):
            - P0 (Critical): medical emergency, stranded with no accommodation, safety concern, unaccompanied minor issue, flight departing within 4 hours AND needs immediate action
            - P1 (High): flight cancelled by airline, missed connection, flight departing within 24 hours AND needs action, lost baggage at destination
            - P2 (Medium): flight delayed, baggage delayed (not lost), schedule change, refund for cancelled flight
            - P3 (Low): general inquiry, future travel question, meal preference, seat preference, feedback/complaint about past flight

            PRIORITY ESCALATION FACTORS (bump up one level if any apply):
            - Passenger tone is distressed, angry, or mentions legal action
            - Flight departure is imminent (within hours)
            - Passenger mentions being stranded, sleeping at airport, or having children/elderly
            - Reservation shows flight status is "cancelled" or "delayed"

            LANGUAGE: The passenger may write in ANY language. Detect the language and write the summary in the SAME language as the passenger's message. Category and priority must always be in English.

            CLARIFYING QUESTIONS: Generate 1-2 short, specific questions relevant to the category. For example:
            - Baggage: "Can you describe your bag and share the baggage claim tag number?"
            - Refund: "Would you prefer a refund to your original payment method or travel credit?"

            Return a JSON object with exactly these fields:
            {
              "summary": "one-line summary in the passenger's language, max 100 chars",
              "category": "exactly one of the category names listed above",
              "priority": "P0, P1, P2, or P3",
              "clarifyingQuestions": ["specific question 1", "specific question 2"]
            }
            """;

        var userMessage = $"Passenger message:\n{passengerFreeText}";
        if (reservationContext != null)
        {
            userMessage += "\n\nReservation context:";
            userMessage += $"\n- Flight status: {reservationContext.FlightStatus}";
            userMessage += $"\n- Refundable: {reservationContext.FareRules.IsRefundable}";
            userMessage += $"\n- Changeable: {reservationContext.FareRules.IsChangeable}";
            if (reservationContext.FareRules.CancellationFee.HasValue)
                userMessage += $"\n- Cancellation fee: ${reservationContext.FareRules.CancellationFee}";
            if (reservationContext.BaggageStatus != null)
                userMessage += $"\n- Baggage status: {reservationContext.BaggageStatus}";
            if (reservationContext.Passengers.Any())
                userMessage += $"\n- Passenger: {reservationContext.Passengers[0].FirstName} {reservationContext.Passengers[0].LastName}";
            foreach (var seg in reservationContext.Segments)
            {
                var timeUntilDeparture = seg.DepartureTime - DateTime.UtcNow;
                userMessage += $"\n- Flight {seg.FlightNumber}: {seg.Departure} → {seg.Arrival}, departs {seg.DepartureTime:yyyy-MM-dd HH:mm} UTC";
                if (timeUntilDeparture.TotalHours is > 0 and < 48)
                    userMessage += $" (DEPARTING IN {timeUntilDeparture.TotalHours:F0} HOURS)";
            }
        }

        try
        {
            var response = await _client.Models.GenerateContentAsync(
                model: _model,
                contents: userMessage,
                config: new GenerateContentConfig
                {
                    SystemInstruction = new Content
                    {
                        Parts = new List<Part> { new() { Text = systemPrompt } }
                    },
                    Temperature = 0.1,
                    MaxOutputTokens = 512,
                    ResponseMimeType = "application/json",
                    ResponseSchema = ClassificationSchema
                },
                cancellationToken: cancellationToken);

            var json = response.Candidates![0].Content!.Parts![0].Text!;
            var parsed = JsonSerializer.Deserialize<ClassificationResponse>(json, JsonOptions)!;

            return new TicketCreationDraft
            {
                Summary = parsed.Summary ?? passengerFreeText[..Math.Min(100, passengerFreeText.Length)],
                CategorySuggested = ParseCategory(parsed.Category),
                PrioritySuggested = ParsePriority(parsed.Priority),
                ClarifyingQuestions = parsed.ClarifyingQuestions ?? new List<string>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini API call failed for ticket classification, falling back to defaults");
            return FallbackClassification(passengerFreeText, reservationContext);
        }
    }

    public async Task<AgentReplyDraft> DraftAgentReplyAsync(
        TicketThreadContext ticketContext,
        ReservationInfo? reservationContext,
        List<PolicyCitation> policyChunks,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = """
            You are a senior airline customer support AI copilot for AirlineSupport.
            You help support agents draft professional replies to passengers.

            TONE & STYLE:
            - Professional yet warm and empathetic
            - Use the passenger's name if available
            - Acknowledge the inconvenience before offering solutions
            - Write in the same language the passenger used in the conversation
            - Keep replies concise but thorough (150-300 words)

            CRITICAL SAFETY RULES:
            - NEVER disclose internal notes (marked as INTERNAL) to the passenger
            - NEVER promise specific compensation amounts — instead say "you may be eligible for compensation, our team will review"
            - NEVER make commitments the agent cannot fulfill — use "I will look into" or "let me check with our team"
            - NEVER share other passengers' information
            - If fare rules indicate non-refundable, do NOT promise a refund — offer alternatives (travel credit, rebooking)

            POLICY CITATIONS:
            - When referencing a company policy, naturally weave it into the reply (e.g., "Per our cancellation policy, ...")
            - List the exact policy section titles you referenced in citedSections

            RISK DETECTION — flag these in riskFlags:
            - Passenger mentions "lawyer", "legal", "sue", "court"
            - Passenger mentions social media, press, or public complaint
            - Passenger has sent 3+ messages without resolution
            - Ticket priority is P0 or P1
            - Flight is cancelled and passenger is stranded
            - Passenger mentions children, elderly, disability, or medical condition

            NEXT ACTIONS — suggest specific steps for the agent:
            - If refund needed: "Process refund via [system] for $X"
            - If rebooking needed: "Check availability on next flight to [destination]"
            - If escalation needed: "Escalate to supervisor — [reason]"

            Return a JSON object with exactly these fields:
            {
              "draftText": "the reply text to send to the passenger",
              "citedSections": ["policy section titles referenced"],
              "missingInfoQuestions": ["questions if more info is needed from passenger"],
              "nextActions": ["specific action items for the agent"],
              "riskFlags": ["risk concerns detected"]
            }
            """;

        var visibleMessages = ticketContext.Messages.Where(m => !m.IsInternal).ToList();
        var internalNotes = ticketContext.Messages.Where(m => m.IsInternal).ToList();

        var conversationHistory = string.Join("\n",
            visibleMessages.Select(m =>
                $"[{m.AuthorName} at {m.CreatedAt:g}]: {m.Content}"));

        var internalContext = internalNotes.Any()
            ? "\n\n=== INTERNAL NOTES (never disclose to passenger) ===\n" +
              string.Join("\n", internalNotes.Select(m => $"[{m.AuthorName}]: {m.Content}"))
            : "";

        var policyContext = policyChunks.Any()
            ? string.Join("\n---\n", policyChunks.Select(p => $"Policy: {p.SectionTitle}\n{p.Content}"))
            : "No relevant policy documents found.";

        var userMessage = $"""
            Ticket #{ticketContext.TicketNumber}
            Subject: {ticketContext.Subject}
            Category: {ticketContext.Category}
            Priority: {ticketContext.Priority}
            State: {ticketContext.State}
            PNR: {ticketContext.PNR ?? "N/A"}
            Messages exchanged: {visibleMessages.Count}

            === Passenger Conversation ===
            {conversationHistory}
            {internalContext}

            === Company Policies ===
            {policyContext}
            """;

        if (reservationContext != null)
        {
            var passengerName = reservationContext.Passengers.FirstOrDefault();
            userMessage += $"""

                === Reservation Details ===
                Passenger: {passengerName?.FirstName} {passengerName?.LastName}
                Flight status: {reservationContext.FlightStatus}
                Refundable: {reservationContext.FareRules.IsRefundable}
                Changeable: {reservationContext.FareRules.IsChangeable}
                Cancellation fee: {reservationContext.FareRules.CancellationFee?.ToString("C") ?? "N/A"}
                """;

            foreach (var seg in reservationContext.Segments)
            {
                userMessage += $"\nFlight {seg.FlightNumber}: {seg.Departure} → {seg.Arrival}, {seg.DepartureTime:yyyy-MM-dd HH:mm} UTC";
            }
        }

        try
        {
            var response = await _client.Models.GenerateContentAsync(
                model: _model,
                contents: userMessage,
                config: new GenerateContentConfig
                {
                    SystemInstruction = new Content
                    {
                        Parts = new List<Part> { new() { Text = systemPrompt } }
                    },
                    Temperature = 0.35,
                    MaxOutputTokens = 1500,
                    ResponseMimeType = "application/json",
                    ResponseSchema = ReplySchema
                },
                cancellationToken: cancellationToken);

            var json = response.Candidates![0].Content!.Parts![0].Text!;
            var parsed = JsonSerializer.Deserialize<ReplyResponse>(json, JsonOptions)!;

            return new AgentReplyDraft
            {
                DraftText = parsed.DraftText ?? "Unable to generate draft reply.",
                Citations = policyChunks
                    .Where(p => parsed.CitedSections?.Contains(p.SectionTitle, StringComparer.OrdinalIgnoreCase) == true)
                    .ToList(),
                MissingInfoQuestions = parsed.MissingInfoQuestions ?? new List<string>(),
                NextActions = parsed.NextActions ?? new List<string>(),
                RiskFlags = parsed.RiskFlags ?? new List<string>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini API call failed for agent reply draft, returning fallback");
            return new AgentReplyDraft
            {
                DraftText = "AI draft generation is temporarily unavailable. Please compose your reply manually.",
                Citations = new List<PolicyCitation>(),
                MissingInfoQuestions = new List<string>(),
                NextActions = new List<string> { "Compose reply manually" },
                RiskFlags = new List<string>()
            };
        }
    }

    private static TicketCategory ParseCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return TicketCategory.General;
        return Enum.TryParse<TicketCategory>(category, ignoreCase: true, out var result)
            ? result
            : TicketCategory.General;
    }

    private static Priority ParsePriority(string? priority)
    {
        if (string.IsNullOrWhiteSpace(priority)) return Priority.P3;
        return Enum.TryParse<Priority>(priority, ignoreCase: true, out var result)
            ? result
            : Priority.P3;
    }

    private static TicketCreationDraft FallbackClassification(string text, ReservationInfo? reservation)
    {
        var lower = text.ToLower();
        var category = lower switch
        {
            _ when lower.Contains("cancel") => TicketCategory.Cancellation,
            _ when lower.Contains("refund") => TicketCategory.Refund,
            _ when lower.Contains("baggage") || lower.Contains("luggage") => TicketCategory.Baggage,
            _ when lower.Contains("delay") => TicketCategory.FlightDelay,
            _ when lower.Contains("seat") => TicketCategory.SeatChange,
            _ => TicketCategory.General
        };

        var priority = reservation?.FlightStatus == "cancelled" ? Priority.P1 : Priority.P3;

        return new TicketCreationDraft
        {
            Summary = text.Length > 100 ? text[..97] + "..." : text,
            CategorySuggested = category,
            PrioritySuggested = priority,
            ClarifyingQuestions = new List<string>()
        };
    }

    private static readonly Schema ClassificationSchema = new()
    {
        Type = Google.GenAI.Types.Type.Object,
        Properties = new Dictionary<string, Schema>
        {
            { "summary", new Schema { Type = Google.GenAI.Types.Type.String } },
            { "category", new Schema { Type = Google.GenAI.Types.Type.String, Enum = new List<string>
                { "General", "Booking", "Cancellation", "Refund", "Baggage", "FlightDelay",
                  "FlightCancellation", "SeatChange", "SpecialAssistance", "MealRequest", "Other" } } },
            { "priority", new Schema { Type = Google.GenAI.Types.Type.String, Enum = new List<string> { "P0", "P1", "P2", "P3" } } },
            { "clarifyingQuestions", new Schema { Type = Google.GenAI.Types.Type.Array, Items = new Schema { Type = Google.GenAI.Types.Type.String } } }
        },
        Required = new List<string> { "summary", "category", "priority", "clarifyingQuestions" }
    };

    private static readonly Schema ReplySchema = new()
    {
        Type = Google.GenAI.Types.Type.Object,
        Properties = new Dictionary<string, Schema>
        {
            { "draftText", new Schema { Type = Google.GenAI.Types.Type.String } },
            { "citedSections", new Schema { Type = Google.GenAI.Types.Type.Array, Items = new Schema { Type = Google.GenAI.Types.Type.String } } },
            { "missingInfoQuestions", new Schema { Type = Google.GenAI.Types.Type.Array, Items = new Schema { Type = Google.GenAI.Types.Type.String } } },
            { "nextActions", new Schema { Type = Google.GenAI.Types.Type.Array, Items = new Schema { Type = Google.GenAI.Types.Type.String } } },
            { "riskFlags", new Schema { Type = Google.GenAI.Types.Type.Array, Items = new Schema { Type = Google.GenAI.Types.Type.String } } }
        },
        Required = new List<string> { "draftText", "citedSections", "missingInfoQuestions", "nextActions", "riskFlags" }
    };

    private record ClassificationResponse(
        string? Summary,
        string? Category,
        string? Priority,
        List<string>? ClarifyingQuestions);

    private record ReplyResponse(
        string? DraftText,
        List<string>? CitedSections,
        List<string>? MissingInfoQuestions,
        List<string>? NextActions,
        List<string>? RiskFlags);
}
