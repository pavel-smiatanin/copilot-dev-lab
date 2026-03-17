namespace UrlShortener.Adapter.Api.Model;

public sealed record CreateLinkResponse(
    Guid Id,
    string Alias,
    string ShortUrl,
    string DestinationUrl,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt,
    bool HasPassword);
