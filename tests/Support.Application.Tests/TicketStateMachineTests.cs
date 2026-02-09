using Support.Domain.Enums;
using Support.Domain.Services;
using Xunit;

namespace Support.Application.Tests;

public class TicketStateMachineTests
{
    [Fact]
    public void New_Can_Transition_To_Triaged()
    {
        var isValid = TicketStateMachine.IsValidTransition(TicketState.New, TicketState.Triaged);
        Assert.True(isValid);
    }

    [Fact]
    public void New_Cannot_Transition_To_Closed()
    {
        var isValid = TicketStateMachine.IsValidTransition(TicketState.New, TicketState.Closed);
        Assert.False(isValid);
    }

    [Fact]
    public void Closed_Is_Terminal_State()
    {
        var nextStates = TicketStateMachine.GetValidNextStates(TicketState.Closed);
        Assert.Empty(nextStates);
    }

    [Theory]
    [InlineData(TicketState.Triaged, TicketState.Assigned, true)]
    [InlineData(TicketState.Assigned, TicketState.InProgress, true)]
    [InlineData(TicketState.InProgress, TicketState.Resolved, true)]
    [InlineData(TicketState.Resolved, TicketState.Closed, true)]
    [InlineData(TicketState.InProgress, TicketState.New, false)]
    public void Test_Various_Transitions(TicketState from, TicketState to, bool expected)
    {
        var isValid = TicketStateMachine.IsValidTransition(from, to);
        Assert.Equal(expected, isValid);
    }
}
