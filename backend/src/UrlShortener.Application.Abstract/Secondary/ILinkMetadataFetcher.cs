namespace UrlShortener.Application.Abstract.Secondary;

/// <summary>
/// Secondary port: fetches HTML metadata (title, OpenGraph tags, favicon) from a destination URL.
/// Implementations must enforce SSRF protection and a 5-second timeout.
/// </summary>
public interface ILinkMetadataFetcher
{
    /// <summary>
    /// Fetches metadata from the given <paramref name="destinationUrl"/>.
    /// Returns <c>null</c> on timeout, network error, or non-HTML content; never throws.
    /// </summary>
    Task<LinkMetadata?> FetchAsync(string destinationUrl, CancellationToken cancellationToken);
}
