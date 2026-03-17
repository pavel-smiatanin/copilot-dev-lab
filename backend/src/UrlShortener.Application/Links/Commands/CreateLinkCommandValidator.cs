using FluentValidation;
using UrlShortener.Application.Abstract.Primary.Commands;
using UrlShortener.Shared.Url;

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

        // SSRF protection: only execute DNS-based check when the URL scheme is already valid
        RuleFor(x => x.DestinationUrl)
            .MustAsync(IsHostSafeAsync)
            .WithMessage("DestinationUrl must not resolve to a private, loopback, or link-local IP address.")
            .When(x => IsValidHttpUrl(x.DestinationUrl));

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

    private static async Task<bool> IsHostSafeAsync(string url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            // Already caught by the IsValidHttpUrl rule above
            return true;
        }

        return await PrivateNetworkGuard.IsHostAllowedAsync(uri.Host, cancellationToken);
    }
}
