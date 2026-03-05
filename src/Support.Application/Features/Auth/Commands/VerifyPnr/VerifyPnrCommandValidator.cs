using FluentValidation;

namespace Support.Application.Features.Auth.Commands.VerifyPnr;

public class VerifyPnrCommandValidator : AbstractValidator<VerifyPnrCommand>
{
    public VerifyPnrCommandValidator()
    {
        RuleFor(x => x.PNR)
            .NotEmpty().WithMessage("PNR is required")
            .MaximumLength(10).WithMessage("PNR must be at most 10 characters");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required")
            .MaximumLength(100).WithMessage("Last name must be at most 100 characters");
    }
}
