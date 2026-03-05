using FluentValidation;

namespace Support.Application.Features.Tickets.Commands.CreateTicket;

public class CreateTicketCommandValidator : AbstractValidator<CreateTicketCommand>
{
    public CreateTicketCommandValidator()
    {
        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Subject is required")
            .MaximumLength(500).WithMessage("Subject must be at most 500 characters");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required")
            .MaximumLength(5000).WithMessage("Description must be at most 5000 characters");

        RuleFor(x => x.PNR)
            .NotEmpty().WithMessage("PNR is required for passenger tickets")
            .MaximumLength(10).WithMessage("PNR must be at most 10 characters");

        RuleFor(x => x.PassengerLastName)
            .NotEmpty().WithMessage("Passenger last name is required")
            .MaximumLength(100).WithMessage("Passenger last name must be at most 100 characters");

        RuleFor(x => x.Category).IsInEnum().WithMessage("Invalid ticket category");
        RuleFor(x => x.Priority).IsInEnum().WithMessage("Invalid priority level");
    }
}
