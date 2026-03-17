using UrlShortener.Application.Abstract.Primary.Commands;

namespace UrlShortener.Adapter.Api.Model;

public sealed record CreateLinkRequest(
    string DestinationUrl,
    string? CustomAlias,
    DateTimeOffset? ExpiresAt,
    string? Password)
{
    public CreateLinkCommand ToCommand() => new(DestinationUrl, CustomAlias, ExpiresAt, Password);
}
