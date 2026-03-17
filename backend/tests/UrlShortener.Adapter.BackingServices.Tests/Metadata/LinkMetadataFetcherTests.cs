using System.Net;
using Moq;
using Serilog;
using UrlShortener.Adapter.BackingServices.Metadata;
using UrlShortener.Application.Abstract.Secondary;

namespace UrlShortener.Adapter.BackingServices.Tests.Metadata;

public sealed class LinkMetadataFetcherTests
{
    private static LinkMetadataFetcher CreateFetcher(HttpMessageHandler handler, ILogger? logger = null)
    {
        HttpClient client = new(handler);
        ILogger log = logger ?? new Mock<ILogger>().Object;
        return new LinkMetadataFetcher(client, log);
    }

    [Fact]
    public async Task FetchAsync_WhenHtmlHasAllMetaTags_ReturnsPopulatedMetadata()
    {
        // Arrange
        const string html = """
            <html><head>
            <title>Test Page &amp; More</title>
            <meta property="og:title" content="OG Title" />
            <meta property="og:image" content="https://example.com/image.jpg" />
            <link rel="icon" href="/favicon.ico" />
            </head><body></body></html>
            """;

        LinkMetadataFetcher fetcher = CreateFetcher(new FakeHttpHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html, System.Text.Encoding.UTF8, "text/html")
            }));

        // Act
        LinkMetadata? result = await fetcher.FetchAsync("https://example.com", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().Be("Test Page & More");
        result.OgTitle.Should().Be("OG Title");
        result.OgImageUrl.Should().Be("https://example.com/image.jpg");
        result.FaviconUrl.Should().Be("https://example.com/favicon.ico");
    }

    [Fact]
    public async Task FetchAsync_WhenHtmlHasNoMetaTags_ReturnsMetadataWithNullFields()
    {
        // Arrange
        const string html = "<html><head></head><body></body></html>";

        LinkMetadataFetcher fetcher = CreateFetcher(new FakeHttpHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html, System.Text.Encoding.UTF8, "text/html")
            }));

        // Act
        LinkMetadata? result = await fetcher.FetchAsync("https://example.com", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().BeNull();
        result.OgTitle.Should().BeNull();
        result.OgImageUrl.Should().BeNull();
        result.FaviconUrl.Should().BeNull();
    }

    [Fact]
    public async Task FetchAsync_WhenHtmlHasTitleOnly_ReturnsTitleAndNullOtherFields()
    {
        // Arrange
        const string html = "<html><head><title>Hello World</title></head><body></body></html>";

        LinkMetadataFetcher fetcher = CreateFetcher(new FakeHttpHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html, System.Text.Encoding.UTF8, "text/html")
            }));

        // Act
        LinkMetadata? result = await fetcher.FetchAsync("https://example.com", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().Be("Hello World");
        result.OgTitle.Should().BeNull();
        result.OgImageUrl.Should().BeNull();
        result.FaviconUrl.Should().BeNull();
    }

    [Fact]
    public async Task FetchAsync_WhenResponseIsNotSuccess_ReturnsNull()
    {
        // Arrange
        LinkMetadataFetcher fetcher = CreateFetcher(new FakeHttpHandler(
            new HttpResponseMessage(HttpStatusCode.NotFound)));

        // Act
        LinkMetadata? result = await fetcher.FetchAsync("https://example.com", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FetchAsync_WhenContentTypeIsNotHtml_ReturnsNull()
    {
        // Arrange
        LinkMetadataFetcher fetcher = CreateFetcher(new FakeHttpHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
            }));

        // Act
        LinkMetadata? result = await fetcher.FetchAsync("https://example.com", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FetchAsync_WhenHttpClientThrows_ReturnsNull()
    {
        // Arrange
        LinkMetadataFetcher fetcher = CreateFetcher(new ThrowingHttpHandler(
            new HttpRequestException("Connection refused")));

        // Act
        LinkMetadata? result = await fetcher.FetchAsync("https://example.com", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FetchAsync_WhenHttpClientThrows_LogsWarning()
    {
        // Arrange
        Mock<ILogger> loggerMock = new();
        LinkMetadataFetcher fetcher = CreateFetcher(
            new ThrowingHttpHandler(new HttpRequestException("Network error")),
            loggerMock.Object);

        // Act
        await fetcher.FetchAsync("https://example.com", CancellationToken.None);

        // Assert — Warning was logged (Serilog uses generic Warning<T> for single-property calls)
        loggerMock.Verify(
            l => l.Warning(It.IsAny<Exception>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task FetchAsync_WhenFaviconHrefIsRelative_ReturnsAbsoluteFaviconUrl()
    {
        // Arrange
        const string html = """
            <html><head>
            <link rel="icon" href="/assets/favicon.png" />
            </head><body></body></html>
            """;

        LinkMetadataFetcher fetcher = CreateFetcher(new FakeHttpHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html, System.Text.Encoding.UTF8, "text/html")
            }));

        // Act
        LinkMetadata? result = await fetcher.FetchAsync("https://example.com/page", CancellationToken.None);

        // Assert — relative favicon resolved to absolute
        result.Should().NotBeNull();
        result!.FaviconUrl.Should().Be("https://example.com/assets/favicon.png");
    }

    // -----------------------------------------------------------------------
    // Fake HTTP handlers
    // -----------------------------------------------------------------------

    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public FakeHttpHandler(HttpResponseMessage response) => _response = response;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_response);
    }

    private sealed class ThrowingHttpHandler : HttpMessageHandler
    {
        private readonly Exception _exception;

        public ThrowingHttpHandler(Exception exception) => _exception = exception;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromException<HttpResponseMessage>(_exception);
    }
}
