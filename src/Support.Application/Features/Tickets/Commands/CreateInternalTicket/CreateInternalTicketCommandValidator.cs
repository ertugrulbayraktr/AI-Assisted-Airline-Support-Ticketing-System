using FluentValidation;

namespace Support.Application.Features.Tickets.Commands.CreateInternalTicket;

public class CreateInternalTicketCommandValidator : AbstractValidator<CreateInternalTicketCommand>
{
    public CreateInternalTicketCommandValidator()
    {
        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Subject is required")
            .MaximumLength(500).WithMessage("Subject must be at most 500 characters");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required")
            .MaximumLength(5000).WithMessage("Description must be at most 5000 characters");

        RuleFor(x => x.PNR)
            .MaximumLength(10).WithMessage("PNR must be at most 10 characters")
            .When(x => x.PNR != null);

        RuleFor(x => x.Category).IsInEnum().WithMessage("Invalid ticket category");
        RuleFor(x => x.Priority).IsInEnum().WithMessage("Invalid priority level");
    }
}
