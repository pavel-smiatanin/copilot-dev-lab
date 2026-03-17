using FluentValidation;
using UrlShortener.Application.Abstract.Primary.Commands;

namespace UrlShortener.Application.Links.Commands;

public sealed class CreateLinkCommandValidator : AbstractValidator<CreateLinkCommand>
{
    private const string AliasPattern = @"^[a-zA-Z0-9_-]{3,50}$";

    public CreateLinkCommandValidator()
    {
        RuleFor(x => x.DestinationUrl)
            .NotEmpty()
            .WithMessage("DestinationUrl is required.")
            .Must(IsValidHttpUrl)
            .WithMessage("DestinationUrl must be a valid http or https URL.");

        RuleFor(x => x.CustomAlias)
            .Matches(AliasPattern)
            .WithMessage("CustomAlias must be 3–50 characters: letters, digits, hyphens, or underscores.")
            .When(x => x.CustomAlias is not null);

        RuleFor(x => x.ExpiresAt)
            .Must(expiry => expiry!.Value > DateTimeOffset.UtcNow)
            .WithMessage("ExpiresAt must be in the future.")
            .When(x => x.ExpiresAt.HasValue);
    }

    private static bool IsValidHttpUrl(string? url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
