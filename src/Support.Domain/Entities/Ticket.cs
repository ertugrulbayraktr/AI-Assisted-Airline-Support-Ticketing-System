using Support.Domain.Common;
using Support.Domain.Enums;
using Support.Domain.Services;

namespace Support.Domain.Entities;

public class Ticket : BaseEntity
{
    public string TicketNumber { get; private set; } = null!;
    public TicketState State { get; private set; }
    public Priority Priority { get; private set; }
    public TicketCategory Category { get; private set; }
    
    public string Subject { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    
    // PNR - optional (agent/internal tickets may not have PNR)
    public string? PNR { get; private set; }
    public string? PassengerLastName { get; private set; }
    
    // Ownership
    public Guid CreatedByUserId { get; private set; }
    public Guid? AssignedToAgentId { get; private set; }
    
    // SLA tracking
    public DateTime FirstResponseDueAt { get; private set; }
    public DateTime ResolutionDueAt { get; private set; }
    public DateTime? FirstResponseAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    
    // Computed
    public bool SlaRisk { get; private set; }
    
    // Navigation
    public List<TicketMessage> Messages { get; private set; } = new();
    public List<TicketAuditEvent> AuditEvents { get; private set; } = new();

    private Ticket() { } // EF Core

    public Ticket(
        string subject,
        string description,
        TicketCategory category,
        Priority priority,
        Guid createdByUserId,
        string? pnr = null,
        string? passengerLastName = null)
    {
        TicketNumber = GenerateTicketNumber();
        Subject = subject;
        Description = description;
        Category = category;
        Priority = priority;
        State = TicketState.New;
        CreatedByUserId = createdByUserId;
        PNR = pnr;
        PassengerLastName = passengerLastName;
        
        // SLA initialization
        FirstResponseDueAt = DateTime.UtcNow.AddHours(2);
        ResolutionDueAt = DateTime.UtcNow.AddHours(24);
        
        ComputeSlaRisk();
    }

    public void Transition(TicketState newState)
    {
        var isValid = TicketStateMachine.IsValidTransition(State, newState);
        if (!isValid)
        {
            throw new InvalidOperationException(
                $"Invalid state transition from {State} to {newState}");
        }

        State = newState;
        
        if (newState == TicketState.Resolved && !ResolvedAt.HasValue)
        {
            ResolvedAt = DateTime.UtcNow;
        }
        
        if (newState == TicketState.Closed && !ClosedAt.HasValue)
        {
            ClosedAt = DateTime.UtcNow;
        }
        
        ComputeSlaRisk();
        UpdateTimestamp();
    }

    public void Assign(Guid agentId)
    {
        AssignedToAgentId = agentId;
        
        // Auto-transition to Assigned from New or Triaged state
        if (State == TicketState.New || State == TicketState.Triaged)
        {
            Transition(TicketState.Assigned);
        }
        
        UpdateTimestamp();
    }

    public void Escalate()
    {
        if (Priority < Priority.P0)
        {
            Priority = (Priority)((int)Priority + 1);
            ComputeSlaRisk();
            UpdateTimestamp();
        }
    }

    public void RecordFirstResponse()
    {
        if (!FirstResponseAt.HasValue)
        {
            FirstResponseAt = DateTime.UtcNow;
            UpdateTimestamp();
        }
    }

    public void ComputeSlaRisk()
    {
        var now = DateTime.UtcNow;
        var riskWindow = TimeSpan.FromMinutes(30);
        
        if (!FirstResponseAt.HasValue)
        {
            SlaRisk = (FirstResponseDueAt - now) <= riskWindow;
        }
        else if (!ResolvedAt.HasValue)
        {
            SlaRisk = (ResolutionDueAt - now) <= riskWindow;
        }
        else
        {
            SlaRisk = false;
        }
    }

    private static string GenerateTicketNumber()
    {
        return $"TKT-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";
    }
}
