using HtmlAgilityPack;
using Serilog;
using UrlShortener.Application.Abstract.Secondary;

namespace UrlShortener.Adapter.BackingServices.Metadata;

/// <summary>
/// Fetches HTML metadata (title, OpenGraph tags, favicon) from a destination URL.
/// Times out after 5 seconds and logs at Warning on any failure; never throws.
/// SSRF protection is enforced via <see cref="SsrfProtectionHandler"/>.
/// </summary>
public sealed class LinkMetadataFetcher : ILinkMetadataFetcher
{
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(5);

    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    public LinkMetadataFetcher(HttpClient httpClient, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<LinkMetadata?> FetchAsync(string destinationUrl, CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeoutCts = new(FetchTimeout);
        using CancellationTokenSource linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            using HttpResponseMessage response =
                await _httpClient.GetAsync(destinationUrl, linkedCts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.Warning(
                    "Metadata fetch returned non-success HTTP {StatusCode} for {Url}",
                    (int)response.StatusCode,
                    destinationUrl);
                return null;
            }

            string? contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType is null ||
                !contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Warning(
                    "Metadata fetch skipped: content type '{ContentType}' is not HTML for {Url}",
                    contentType,
                    destinationUrl);
                return null;
            }

            string html = await response.Content.ReadAsStringAsync(linkedCts.Token);
            return ParseMetadata(html, destinationUrl);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested
                                                  && !cancellationToken.IsCancellationRequested)
        {
            _logger.Warning(
                "Metadata fetch timed out after {TimeoutSeconds}s for {Url}",
                FetchTimeout.TotalSeconds,
                destinationUrl);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Warning(ex, "Metadata fetch failed for {Url}", destinationUrl);
            return null;
        }
    }

    private static LinkMetadata ParseMetadata(string html, string baseUrl)
    {
        HtmlDocument doc = new();
        doc.LoadHtml(html);

        string? title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim();
        if (!string.IsNullOrEmpty(title))
        {
            title = HtmlEntity.DeEntitize(title);
        }

        string? ogTitle = GetMetaContent(doc, "og:title");
        string? ogImageUrl = GetMetaContent(doc, "og:image");
        string? faviconUrl = GetFaviconUrl(doc, baseUrl);

        return new LinkMetadata(
            string.IsNullOrWhiteSpace(title) ? null : title,
            ogTitle,
            ogImageUrl,
            faviconUrl);
    }

    private static string? GetMetaContent(HtmlDocument doc, string property)
    {
        HtmlNode? node = doc.DocumentNode
            .SelectSingleNode($"//meta[@property='{property}']");
        string? content = node?.GetAttributeValue("content", null);
        return string.IsNullOrWhiteSpace(content) ? null : content;
    }

    private static string? GetFaviconUrl(HtmlDocument doc, string baseUrl)
    {
        // Try <link rel="icon">, <link rel="shortcut icon">, <link rel="apple-touch-icon">
        HtmlNode? node = doc.DocumentNode.SelectSingleNode(
            "//link[contains(concat(' ', normalize-space(@rel), ' '), ' icon ')]");

        string? href = node?.GetAttributeValue("href", null);
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        // Resolve relative URLs to absolute
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? baseUri) &&
            Uri.TryCreate(baseUri, href, out Uri? absoluteUri))
        {
            return absoluteUri.ToString();
        }

        return href;
    }
}
