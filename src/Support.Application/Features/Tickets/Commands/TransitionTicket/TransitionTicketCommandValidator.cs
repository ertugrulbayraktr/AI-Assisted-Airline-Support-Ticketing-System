using FluentValidation;

namespace Support.Application.Features.Tickets.Commands.TransitionTicket;

public class TransitionTicketCommandValidator : AbstractValidator<TransitionTicketCommand>
{
    public TransitionTicketCommandValidator()
    {
        RuleFor(x => x.NewState)
            .IsInEnum().WithMessage("Invalid ticket state");
    }
}
