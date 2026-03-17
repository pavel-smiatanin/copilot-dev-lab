namespace UrlShortener.Application.Abstract.Primary.Commands;

public sealed record CreateLinkResult(
    Guid Id,
    string Alias,
    string DestinationUrl,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt,
    bool HasPassword);
