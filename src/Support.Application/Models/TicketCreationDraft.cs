using Support.Domain.Enums;

namespace Support.Application.Models;

public class TicketCreationDraft
{
    public string Summary { get; set; } = null!;
    public TicketCategory CategorySuggested { get; set; }
    public Priority PrioritySuggested { get; set; }
    public List<string> ClarifyingQuestions { get; set; } = new();
}
