using Support.Domain.Enums;

namespace Support.Domain.Services;

public static class TicketStateMachine
{
    private static readonly Dictionary<TicketState, List<TicketState>> ValidTransitions = new()
    {
        { TicketState.New, new List<TicketState> { TicketState.Triaged, TicketState.Assigned, TicketState.Cancelled } },
        { TicketState.Triaged, new List<TicketState> { TicketState.Assigned, TicketState.Cancelled } },
        { TicketState.Assigned, new List<TicketState> { TicketState.InProgress, TicketState.Cancelled } },
        { TicketState.InProgress, new List<TicketState> { 
            TicketState.WaitingOnPassenger, 
            TicketState.Resolved, 
            TicketState.Cancelled 
        }},
        { TicketState.WaitingOnPassenger, new List<TicketState> { 
            TicketState.InProgress, 
            TicketState.Resolved, 
            TicketState.Cancelled 
        }},
        { TicketState.Resolved, new List<TicketState> { 
            TicketState.Closed, 
            TicketState.InProgress // Reopen if needed
        }},
        { TicketState.Closed, new List<TicketState>() }, // Terminal
        { TicketState.Cancelled, new List<TicketState>() } // Terminal
    };

    public static bool IsValidTransition(TicketState from, TicketState to)
    {
        return ValidTransitions.ContainsKey(from) && ValidTransitions[from].Contains(to);
    }

    public static List<TicketState> GetValidNextStates(TicketState currentState)
    {
        return ValidTransitions.ContainsKey(currentState) 
            ? ValidTransitions[currentState] 
            : new List<TicketState>();
    }
}
