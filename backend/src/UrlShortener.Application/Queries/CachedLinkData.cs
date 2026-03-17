namespace UrlShortener.Application.Queries;

/// <summary>
/// Serialized payload stored in Redis under the <c>alias:{shortId}</c> cache key.
/// Contains the minimum data required to resolve a redirect without hitting the database.
/// </summary>
internal sealed record CachedLinkData(
    Guid LinkId,
    string DestinationUrl,
    DateTimeOffset? ExpiresAt,
    bool HasPassword);
