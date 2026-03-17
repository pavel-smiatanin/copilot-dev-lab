namespace UrlShortener.Application.Abstract.Secondary;

/// <summary>
/// Metadata extracted from the destination URL during link creation.
/// All fields are optional — fetching is best-effort.
/// </summary>
public sealed record LinkMetadata(
    string? Title,
    string? OgTitle,
    string? OgImageUrl,
    string? FaviconUrl);
