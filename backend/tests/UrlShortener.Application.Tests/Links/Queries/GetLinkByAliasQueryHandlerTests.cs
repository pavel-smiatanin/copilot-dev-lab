using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Serilog;
using UrlShortener.Application.Abstract.Model;
using UrlShortener.Application.Abstract.Primary.Queries;
using UrlShortener.Application.Abstract.Secondary;
using UrlShortener.Application.Queries;

namespace UrlShortener.Application.Tests.Links.Queries;

internal sealed class TestAppDbContext : AppDbContext
{
    public TestAppDbContext(DbContextOptions<TestAppDbContext> options) : base(options) { }
}

public sealed class GetLinkByAliasQueryHandlerTests
{
    private static TestAppDbContext CreateDbContext()
    {
        DbContextOptions<TestAppDbContext> options = new DbContextOptionsBuilder<TestAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestAppDbContext(options);
    }

    private static GetLinkByAliasQueryHandler CreateHandler(
        TestAppDbContext dbContext,
        Mock<IDistributedCache> cacheMock,
        TimeProvider? timeProvider = null)
    {
        Mock<ILogger> loggerMock = new();
        Mock<IUnlockTokenService> tokenServiceMock = new();
        return new GetLinkByAliasQueryHandler(
            dbContext,
            cacheMock.Object,
            tokenServiceMock.Object,
            timeProvider ?? TimeProvider.System,
            loggerMock.Object);
    }

    private static byte[] SerializeCacheEntry(
        Guid linkId,
        string destinationUrl,
        DateTimeOffset? expiresAt,
        bool hasPassword)
    {
        CachedLinkData data = new(linkId, destinationUrl, expiresAt, hasPassword);
        string json = JsonSerializer.Serialize(data);
        return Encoding.UTF8.GetBytes(json);
    }

    private static void SetupCacheHit(Mock<IDistributedCache> cacheMock, byte[] cachedBytes)
    {
        cacheMock
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedBytes);
    }

    private static void SetupCacheMiss(Mock<IDistributedCache> cacheMock)
    {
        cacheMock
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Cache-hit scenarios
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_CacheHit_NonExpiredLink_ReturnsRedirect()
    {
        // Arrange
        using TestAppDbContext dbContext = CreateDbContext();
        Mock<IDistributedCache> cacheMock = new();
        byte[] bytes = SerializeCacheEntry(Guid.NewGuid(), "https://example.com", null, false);
        SetupCacheHit(cacheMock, bytes);

        GetLinkByAliasQueryHandler handler = CreateHandler(dbContext, cacheMock);

        // Act
        GetLinkByAliasResult result = await handler.Handle(
            new GetLinkByAliasQuery("abc123", null),
            CancellationToken.None);

        // Assert
        result.Should().BeOfType<GetLinkByAliasResult.Redirect>();
        ((GetLinkByAliasResult.Redirect)result).DestinationUrl.Should().Be("https://example.com");
    }

    [Fact]
    public async Task Handle_CacheHit_ExpiredLink_ReturnsNotFound()
    {
        // Arrange
        using TestAppDbContext dbContext = CreateDbContext();
        Mock<IDistributedCache> cacheMock = new();
        DateTimeOffset expiredAt = DateTimeOffset.UtcNow.AddHours(-1);
        byte[] bytes = SerializeCacheEntry(Guid.NewGuid(), "https://example.com", expiredAt, false);
        SetupCacheHit(cacheMock, bytes);

        GetLinkByAliasQueryHandler handler = CreateHandler(dbContext, cacheMock);

        // Act
        GetLinkByAliasResult result = await handler.Handle(
            new GetLinkByAliasQuery("abc123", null),
            CancellationToken.None);

        // Assert
        result.Should().BeOfType<GetLinkByAliasResult.NotFound>();
    }

    [Fact]
    public async Task Handle_CacheHit_PasswordProtectedLink_ReturnsRequiresUnlock()
    {
        // Arrange
        using TestAppDbContext dbContext = CreateDbContext();
        Mock<IDistributedCache> cacheMock = new();
        byte[] bytes = SerializeCacheEntry(Guid.NewGuid(), "https://example.com", null, hasPassword: true);
        SetupCacheHit(cacheMock, bytes);

        GetLinkByAliasQueryHandler handler = CreateHandler(dbContext, cacheMock);

        // Act
        GetLinkByAliasResult result = await handler.Handle(
            new GetLinkByAliasQuery("abc123", null),
            CancellationToken.None);

        // Assert
        result.Should().BeOfType<GetLinkByAliasResult.RequiresUnlock>();
    }

    [Fact]
    public async Task Handle_CacheHit_DoesNotQueryDatabase()
    {
        // Arrange
        using TestAppDbContext dbContext = CreateDbContext();

        // Populate DB with a link that should NOT be queried
        dbContext.Links.Add(new Link
        {
            Id = Guid.NewGuid(),
            Alias = "abc123",
            DestinationUrl = "https://db.example.com",
            CreatedAt = DateTimeOffset.UtcNow,
            RowVersion = []
        });
        await dbContext.SaveChangesAsync();

        Mock<IDistributedCache> cacheMock = new();
        // Cache returns different URL — if cache is used, we get "https://cached.example.com"
        byte[] bytes = SerializeCacheEntry(Guid.NewGuid(), "https://cached.example.com", null, false);
        SetupCacheHit(cacheMock, bytes);

        GetLinkByAliasQueryHandler handler = CreateHandler(dbContext, cacheMock);

        // Act
        GetLinkByAliasResult result = await handler.Handle(
            new GetLinkByAliasQuery("abc123", null),
            CancellationToken.None);

        // Assert — returns the cached value, not the DB value
        result.Should().BeOfType<GetLinkByAliasResult.Redirect>();
        ((GetLinkByAliasResult.Redirect)result).DestinationUrl.Should().Be("https://cached.example.com");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Cache-miss + DB scenarios
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_CacheMiss_AliasNotInDb_ReturnsNotFound()
    {
        // Arrange
        using TestAppDbContext dbContext = CreateDbContext();
        Mock<IDistributedCache> cacheMock = new();
        SetupCacheMiss(cacheMock);

        GetLinkByAliasQueryHandler handler = CreateHandler(dbContext, cacheMock);

        // Act
        GetLinkByAliasResult result = await handler.Handle(
            new GetLinkByAliasQuery("notfound", null),
            CancellationToken.None);

        // Assert
        result.Should().BeOfType<GetLinkByAliasResult.NotFound>();
    }

    [Fact]
    public async Task Handle_CacheMiss_ValidNonExpiredLink_ReturnsRedirect()
    {
        // Arrange
        using TestAppDbContext dbContext = CreateDbContext();
        Guid linkId = Guid.NewGuid();
        dbContext.Links.Add(new Link
        {
            Id = linkId,
            Alias = "myalias",
            DestinationUrl = "https://example.com/target",
            CreatedAt = DateTimeOffset.UtcNow,
            RowVersion = []
        });
        await dbContext.SaveChangesAsync();

        Mock<IDistributedCache> cacheMock = new();
        SetupCacheMiss(cacheMock);

        GetLinkByAliasQueryHandler handler = CreateHandler(dbContext, cacheMock);

        // Act
        GetLinkByAliasResult result = await handler.Handle(
            new GetLinkByAliasQuery("myalias", null),
            CancellationToken.None);

        // Assert
        result.Should().BeOfType<GetLinkByAliasResult.Redirect>();
        ((GetLinkByAliasResult.Redirect)result).DestinationUrl.Should().Be("https://example.com/target");
    }

    [Fact]
    public async Task Handle_CacheMiss_ExpiredLink_ReturnsNotFound()
    {
        // Arrange
        using TestAppDbContext dbContext = CreateDbContext();
        dbContext.Links.Add(new Link
        {
            Id = Guid.NewGuid(),
            Alias = "expired",
            DestinationUrl = "https://example.com",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            RowVersion = []
        });
        await dbContext.SaveChangesAsync();

        Mock<IDistributedCache> cacheMock = new();
        SetupCacheMiss(cacheMock);

        GetLinkByAliasQueryHandler handler = CreateHandler(dbContext, cacheMock);

        // Act
        GetLinkByAliasResult result = await handler.Handle(
            new GetLinkByAliasQuery("expired", null),
            CancellationToken.None);

        // Assert
        result.Should().BeOfType<GetLinkByAliasResult.NotFound>();
    }

    [Fact]
    public async Task Handle_CacheMiss_PasswordProtectedLink_ReturnsRequiresUnlock()
    {
        // Arrange
        using TestAppDbContext dbContext = CreateDbContext();
        dbContext.Links.Add(new Link
        {
            Id = Guid.NewGuid(),
            Alias = "locked",
            DestinationUrl = "https://example.com/secret",
            PasswordHash = "$2a$12$hashedpassword",
            CreatedAt = DateTimeOffset.UtcNow,
            RowVersion = []
        });
        await dbContext.SaveChangesAsync();

        Mock<IDistributedCache> cacheMock = new();
        SetupCacheMiss(cacheMock);

        GetLinkByAliasQueryHandler handler = CreateHandler(dbContext, cacheMock);

        // Act
        GetLinkByAliasResult result = await handler.Handle(
            new GetLinkByAliasQuery("locked", null),
            CancellationToken.None);

        // Assert
        result.Should().BeOfType<GetLinkByAliasResult.RequiresUnlock>();
    }

    [Fact]
    public async Task Handle_CacheMiss_ValidLink_PopulatesCache()
    {
        // Arrange
        using TestAppDbContext dbContext = CreateDbContext();
        dbContext.Links.Add(new Link
        {
            Id = Guid.NewGuid(),
            Alias = "populate",
            DestinationUrl = "https://example.com",
            CreatedAt = DateTimeOffset.UtcNow,
            RowVersion = []
        });
        await dbContext.SaveChangesAsync();

        Mock<IDistributedCache> cacheMock = new();
        SetupCacheMiss(cacheMock);

        GetLinkByAliasQueryHandler handler = CreateHandler(dbContext, cacheMock);

        // Act
        await handler.Handle(new GetLinkByAliasQuery("populate", null), CancellationToken.None);

        // Assert — SetAsync was called to populate the cache
        cacheMock.Verify(
            c => c.SetAsync(
                It.Is<string>(k => k.Contains("populate")),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_CacheMiss_ExpiredLink_DoesNotPopulateCache()
    {
        // Arrange
        using TestAppDbContext dbContext = CreateDbContext();
        dbContext.Links.Add(new Link
        {
            Id = Guid.NewGuid(),
            Alias = "expiredlink",
            DestinationUrl = "https://example.com",
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            RowVersion = []
        });
        await dbContext.SaveChangesAsync();

        Mock<IDistributedCache> cacheMock = new();
        SetupCacheMiss(cacheMock);

        GetLinkByAliasQueryHandler handler = CreateHandler(dbContext, cacheMock);

        // Act
        await handler.Handle(new GetLinkByAliasQuery("expiredlink", null), CancellationToken.None);

        // Assert — SetAsync was NOT called because the link is expired
        cacheMock.Verify(
            c => c.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Redis failure / graceful degradation
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_CacheReadThrows_FallsBackToDatabase()
    {
        // Arrange
        using TestAppDbContext dbContext = CreateDbContext();
        dbContext.Links.Add(new Link
        {
            Id = Guid.NewGuid(),
            Alias = "fallback",
            DestinationUrl = "https://example.com/fallback",
            CreatedAt = DateTimeOffset.UtcNow,
            RowVersion = []
        });
        await dbContext.SaveChangesAsync();

        Mock<IDistributedCache> cacheMock = new();
        cacheMock
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis unavailable"));

        GetLinkByAliasQueryHandler handler = CreateHandler(dbContext, cacheMock);

        // Act
        GetLinkByAliasResult result = await handler.Handle(
            new GetLinkByAliasQuery("fallback", null),
            CancellationToken.None);

        // Assert — DB fallback succeeded
        result.Should().BeOfType<GetLinkByAliasResult.Redirect>();
        ((GetLinkByAliasResult.Redirect)result).DestinationUrl.Should().Be("https://example.com/fallback");
    }

    [Fact]
    public async Task Handle_CacheWriteThrows_StillReturnsCorrectResult()
    {
        // Arrange
        using TestAppDbContext dbContext = CreateDbContext();
        dbContext.Links.Add(new Link
        {
            Id = Guid.NewGuid(),
            Alias = "writefail",
            DestinationUrl = "https://example.com/writefail",
            CreatedAt = DateTimeOffset.UtcNow,
            RowVersion = []
        });
        await dbContext.SaveChangesAsync();

        Mock<IDistributedCache> cacheMock = new();
        SetupCacheMiss(cacheMock);
        cacheMock
            .Setup(c => c.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis unavailable"));

        GetLinkByAliasQueryHandler handler = CreateHandler(dbContext, cacheMock);

        // Act — should not throw even if cache write fails
        GetLinkByAliasResult result = await handler.Handle(
            new GetLinkByAliasQuery("writefail", null),
            CancellationToken.None);

        // Assert
        result.Should().BeOfType<GetLinkByAliasResult.Redirect>();
        ((GetLinkByAliasResult.Redirect)result).DestinationUrl.Should().Be("https://example.com/writefail");
    }
}
