using Microsoft.EntityFrameworkCore;
using Moq;
using Serilog;
using UrlShortener.Application.Abstract.Primary.Commands;
using UrlShortener.Application.Abstract.Secondary;
using UrlShortener.Application.Commands;

namespace UrlShortener.Application.Tests.Links.Commands;

/// <summary>
/// Minimal in-memory DbContext for handler tests.
/// </summary>
internal sealed class TestAppDbContext : AppDbContext
{
    public TestAppDbContext(DbContextOptions<TestAppDbContext> options) : base(options) { }
}

public sealed class CreateLinkCommandHandlerTests
{
    private static TestAppDbContext CreateDbContext()
    {
        DbContextOptions<TestAppDbContext> options = new DbContextOptionsBuilder<TestAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestAppDbContext(options);
    }

    private static CreateLinkCommandHandler CreateHandler(
        TestAppDbContext dbContext,
        Mock<ILinkMetadataFetcher>? metadataFetcherMock = null)
    {
        metadataFetcherMock ??= new Mock<ILinkMetadataFetcher>();
        Mock<ILogger> loggerMock = new();
        return new CreateLinkCommandHandler(
            dbContext,
            TimeProvider.System,
            metadataFetcherMock.Object,
            loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WhenMetadataFetchReturnsData_SavesMetadataFieldsOnLink()
    {
        // Arrange
        using TestAppDbContext dbContext = CreateDbContext();
        Mock<ILinkMetadataFetcher> fetcherMock = new();
        LinkMetadata metadata = new("Page Title", "OG Title", "https://img.example.com/og.png", "https://example.com/favicon.ico");
        fetcherMock
            .Setup(f => f.FetchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);

        CreateLinkCommandHandler handler = CreateHandler(dbContext, fetcherMock);

        CreateLinkCommand command = new("https://example.com/some/path", null, null, null);

        // Act
        CreateLinkResult result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Abstract.Model.Link? saved = await dbContext.Links.FindAsync(result.Id);
        saved.Should().NotBeNull();
        saved!.Title.Should().Be("Page Title");
        saved.OgTitle.Should().Be("OG Title");
        saved.OgImageUrl.Should().Be("https://img.example.com/og.png");
        saved.FaviconUrl.Should().Be("https://example.com/favicon.ico");
    }

    [Fact]
    public async Task Handle_WhenMetadataFetchReturnsNull_SavesLinkWithNullMetadataFields()
    {
        // Arrange
        using TestAppDbContext dbContext = CreateDbContext();
        Mock<ILinkMetadataFetcher> fetcherMock = new();
        fetcherMock
            .Setup(f => f.FetchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LinkMetadata?)null);

        CreateLinkCommandHandler handler = CreateHandler(dbContext, fetcherMock);
        CreateLinkCommand command = new("https://example.com/path", null, null, null);

        // Act
        CreateLinkResult result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Abstract.Model.Link? saved = await dbContext.Links.FindAsync(result.Id);
        saved.Should().NotBeNull();
        saved!.Title.Should().BeNull();
        saved.OgTitle.Should().BeNull();
        saved.OgImageUrl.Should().BeNull();
        saved.FaviconUrl.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenMetadataFetchFails_LinkIsStillSaved()
    {
        // Arrange
        using TestAppDbContext dbContext = CreateDbContext();
        Mock<ILinkMetadataFetcher> fetcherMock = new();
        fetcherMock
            .Setup(f => f.FetchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LinkMetadata?)null); // simulates failure path returning null

        CreateLinkCommandHandler handler = CreateHandler(dbContext, fetcherMock);
        CreateLinkCommand command = new("https://example.com/page", "myalias", null, null);

        // Act
        CreateLinkResult result = await handler.Handle(command, CancellationToken.None);

        // Assert — link was saved despite no metadata
        result.Alias.Should().Be("myalias");
        dbContext.Links.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_WhenMetadataFetched_InvokesMetadataFetcherWithDestinationUrl()
    {
        // Arrange
        using TestAppDbContext dbContext = CreateDbContext();
        Mock<ILinkMetadataFetcher> fetcherMock = new();
        fetcherMock
            .Setup(f => f.FetchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LinkMetadata?)null);

        CreateLinkCommandHandler handler = CreateHandler(dbContext, fetcherMock);
        const string destinationUrl = "https://example.com/target";
        CreateLinkCommand command = new(destinationUrl, null, null, null);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert — fetcher was called with the correct URL
        fetcherMock.Verify(
            f => f.FetchAsync(destinationUrl, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
