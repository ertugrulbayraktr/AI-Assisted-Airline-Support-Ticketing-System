using FluentValidation;

namespace Support.Application.Features.Tickets.Commands.AddMessage;

public class AddMessageCommandValidator : AbstractValidator<AddMessageCommand>
{
    public AddMessageCommandValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Message content is required")
            .MaximumLength(10000).WithMessage("Message content must be at most 10000 characters");
    }
}
