using FluentValidation;
using UrlShortener.Application.Abstract.Primary.Commands;

namespace UrlShortener.Application.Commands;

public sealed class UnlockLinkCommandValidator : AbstractValidator<UnlockLinkCommand>
{
    public UnlockLinkCommandValidator()
    {
        RuleFor(x => x.Alias)
            .NotEmpty()
            .Matches(@"^[a-zA-Z0-9_-]{3,50}$")
            .WithMessage("Alias must be 3–50 characters using letters, digits, hyphens, or underscores.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is required.");
    }
}
