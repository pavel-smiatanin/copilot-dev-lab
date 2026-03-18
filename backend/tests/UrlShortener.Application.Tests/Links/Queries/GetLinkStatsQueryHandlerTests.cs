using Microsoft.EntityFrameworkCore;
using Moq;
using Serilog;
using UrlShortener.Application.Abstract.Model;
using UrlShortener.Application.Abstract.Primary.Queries;
using UrlShortener.Application.Queries;

namespace UrlShortener.Application.Tests.Links.Queries;

// TestAppDbContext is defined in GetLinkByAliasQueryHandlerTests.cs (same namespace).

public sealed class GetLinkStatsQueryHandlerTests
{
    private static TestAppDbContext CreateDbContext()
    {
        DbContextOptions<TestAppDbContext> options = new DbContextOptionsBuilder<TestAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestAppDbContext(options);
    }

    private static GetLinkStatsQueryHandler CreateHandler(
        TestAppDbContext dbContext,
        TimeProvider? timeProvider = null)
    {
        Mock<ILogger> loggerMock = new();
        return new GetLinkStatsQueryHandler(
            dbContext,
            timeProvider ?? TimeProvider.System,
            loggerMock.Object);
    }

    private static TimeProvider CreateFixedTimeProvider(DateTimeOffset now)
    {
        Mock<TimeProvider> mock = new();
        mock.Setup(p => p.GetUtcNow()).Returns(now);
        return mock.Object;
    }

    private static Link CreateLink(Guid id, string alias = "test123")
    {
        return new Link
        {
            Id = id,
            Alias = alias,
            DestinationUrl = "https://example.com",
            CreatedAt = DateTimeOffset.UtcNow,
            RowVersion = []
        };
    }

    private static Visit CreateVisit(
        Guid linkId,
        DateTimeOffset occurredAt,
        string hashedIp = "hashed_ip_1",
        string? referrerHost = null)
    {
        return new Visit
        {
            Id = Guid.NewGuid(),
            LinkId = linkId,
            OccurredAt = occurredAt,
            HashedIp = hashedIp,
            ReferrerHost = referrerHost
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Link not found
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenLinkDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        using TestAppDbContext dbContext = CreateDbContext();
        GetLinkStatsQueryHandler handler = CreateHandler(dbContext);

        // Act
        GetLinkStatsResult result = await handler.Handle(
            new GetLinkStatsQuery(Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        result.Should().BeOfType<GetLinkStatsResult.NotFound>();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Zero visits
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenLinkHasNoVisits_ReturnsFoundWithZeroCounts()
    {
        // Arrange
        using TestAppDbContext dbContext = CreateDbContext();
        Guid linkId = Guid.NewGuid();
        await dbContext.Links.AddAsync(CreateLink(linkId));
        await dbContext.SaveChangesAsync();

        GetLinkStatsQueryHandler handler = CreateHandler(dbContext);

        // Act
        GetLinkStatsResult result = await handler.Handle(
            new GetLinkStatsQuery(linkId),
            CancellationToken.None);

        // Assert
        var found = result.Should().BeOfType<GetLinkStatsResult.Found>().Subject;
        found.Stats.TotalVisits.Should().Be(0);
        found.Stats.UniqueVisitors.Should().Be(0);
        found.Stats.VisitsByDay.Should().BeEmpty();
        found.Stats.TopReferrers.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Total visits
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(50)]
    public async Task Handle_WhenLinkHasVisits_ReturnsTotalVisitCount(int visitCount)
    {
        // Arrange
        using TestAppDbContext dbContext = CreateDbContext();
        Guid linkId = Guid.NewGuid();
        await dbContext.Links.AddAsync(CreateLink(linkId));

        DateTimeOffset baseTime = DateTimeOffset.UtcNow;
        for (int i = 0; i < visitCount; i++)
        {
            await dbContext.Visits.AddAsync(
                CreateVisit(linkId, baseTime.AddHours(-i), hashedIp: $"ip_{i}"));
        }

        await dbContext.SaveChangesAsync();

        GetLinkStatsQueryHandler handler = CreateHandler(dbContext, CreateFixedTimeProvider(baseTime));

        // Act
        GetLinkStatsResult result = await handler.Handle(
            new GetLinkStatsQuery(linkId),
            CancellationToken.None);

        // Assert
        var found = result.Should().BeOfType<GetLinkStatsResult.Found>().Subject;
        found.Stats.TotalVisits.Should().Be(visitCount);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Unique visitor counting
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenVisitsHaveDuplicateHashedIps_CountsUniqueVisitors()
    {
        // Arrange
        using TestAppDbContext dbContext = CreateDbContext();
        Guid linkId = Guid.NewGuid();
        await dbContext.Links.AddAsync(CreateLink(linkId));

        DateTimeOffset now = DateTimeOffset.UtcNow;
        // 3 visits from ip_1, 2 from ip_2, 1 from ip_3 → 3 unique
        await dbContext.Visits.AddRangeAsync([
            CreateVisit(linkId, now, "ip_1"),
            CreateVisit(linkId, now.AddMinutes(-1), "ip_1"),
            CreateVisit(linkId, now.AddMinutes(-2), "ip_1"),
            CreateVisit(linkId, now.AddMinutes(-3), "ip_2"),
            CreateVisit(linkId, now.AddMinutes(-4), "ip_2"),
            CreateVisit(linkId, now.AddMinutes(-5), "ip_3")
        ]);

        await dbContext.SaveChangesAsync();

        GetLinkStatsQueryHandler handler = CreateHandler(dbContext, CreateFixedTimeProvider(now));

        // Act
        GetLinkStatsResult result = await handler.Handle(
            new GetLinkStatsQuery(linkId),
            CancellationToken.None);

        // Assert
        var found = result.Should().BeOfType<GetLinkStatsResult.Found>().Subject;
        found.Stats.TotalVisits.Should().Be(6);
        found.Stats.UniqueVisitors.Should().Be(3);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Visits-by-day: 30-day window
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_VisitsByDay_ExcludesVisitsOlderThan30Days()
    {
        // Arrange
        using TestAppDbContext dbContext = CreateDbContext();
        Guid linkId = Guid.NewGuid();
        await dbContext.Links.AddAsync(CreateLink(linkId));

        DateTimeOffset now = new(2026, 3, 18, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset within30Days = now.AddDays(-15);   // included
        DateTimeOffset exactly31DaysAgo = now.AddDays(-31); // excluded (older than cutoff)

        await dbContext.Visits.AddRangeAsync([
            CreateVisit(linkId, within30Days),
            CreateVisit(linkId, exactly31DaysAgo)
        ]);

        await dbContext.SaveChangesAsync();

        GetLinkStatsQueryHandler handler = CreateHandler(dbContext, CreateFixedTimeProvider(now));

        // Act
        GetLinkStatsResult result = await handler.Handle(
            new GetLinkStatsQuery(linkId),
            CancellationToken.None);

        // Assert
        var found = result.Should().BeOfType<GetLinkStatsResult.Found>().Subject;
        found.Stats.VisitsByDay.Should().HaveCount(1);
        found.Stats.VisitsByDay[0].Date.Should().Be(DateOnly.FromDateTime(within30Days.DateTime));
    }

    [Fact]
    public async Task Handle_VisitsByDay_GroupsMultipleVisitsByDate()
    {
        // Arrange
        using TestAppDbContext dbContext = CreateDbContext();
        Guid linkId = Guid.NewGuid();
        await dbContext.Links.AddAsync(CreateLink(linkId));

        DateTimeOffset now = new(2026, 3, 18, 12, 0, 0, TimeSpan.Zero);
        // Day 1: 13-Mar (3 visits), Day 2: 15-Mar (2 visits)
        DateTimeOffset day1 = new(2026, 3, 13, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset day2 = new(2026, 3, 15, 14, 0, 0, TimeSpan.Zero);

        await dbContext.Visits.AddRangeAsync([
            CreateVisit(linkId, day1),
            CreateVisit(linkId, day1.AddHours(1)),
            CreateVisit(linkId, day1.AddHours(2)),
            CreateVisit(linkId, day2),
            CreateVisit(linkId, day2.AddHours(3))
        ]);

        await dbContext.SaveChangesAsync();

        GetLinkStatsQueryHandler handler = CreateHandler(dbContext, CreateFixedTimeProvider(now));

        // Act
        GetLinkStatsResult result = await handler.Handle(
            new GetLinkStatsQuery(linkId),
            CancellationToken.None);

        // Assert
        var found = result.Should().BeOfType<GetLinkStatsResult.Found>().Subject;
        found.Stats.VisitsByDay.Should().HaveCount(2);

        VisitsByDay day1Entry = found.Stats.VisitsByDay
            .Single(d => d.Date == new DateOnly(2026, 3, 13));
        day1Entry.Count.Should().Be(3);

        VisitsByDay day2Entry = found.Stats.VisitsByDay
            .Single(d => d.Date == new DateOnly(2026, 3, 15));
        day2Entry.Count.Should().Be(2);
    }

    [Fact]
    public async Task Handle_VisitsByDay_AreReturnedInAscendingDateOrder()
    {
        // Arrange
        using TestAppDbContext dbContext = CreateDbContext();
        Guid linkId = Guid.NewGuid();
        await dbContext.Links.AddAsync(CreateLink(linkId));

        DateTimeOffset now = new(2026, 3, 18, 12, 0, 0, TimeSpan.Zero);
        await dbContext.Visits.AddRangeAsync([
            CreateVisit(linkId, now.AddDays(-1)),
            CreateVisit(linkId, now.AddDays(-5)),
            CreateVisit(linkId, now.AddDays(-10)),
            CreateVisit(linkId, now.AddDays(-3))
        ]);
        await dbContext.SaveChangesAsync();

        GetLinkStatsQueryHandler handler = CreateHandler(dbContext, CreateFixedTimeProvider(now));

        // Act
        GetLinkStatsResult result = await handler.Handle(
            new GetLinkStatsQuery(linkId),
            CancellationToken.None);

        // Assert
        var found = result.Should().BeOfType<GetLinkStatsResult.Found>().Subject;
        found.Stats.VisitsByDay.Select(d => d.Date).Should().BeInAscendingOrder();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Top referrers
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_TopReferrers_ExcludesNullReferrers()
    {
        // Arrange
        using TestAppDbContext dbContext = CreateDbContext();
        Guid linkId = Guid.NewGuid();
        await dbContext.Links.AddAsync(CreateLink(linkId));

        DateTimeOffset now = DateTimeOffset.UtcNow;
        await dbContext.Visits.AddRangeAsync([
            CreateVisit(linkId, now, referrerHost: null),
            CreateVisit(linkId, now.AddMinutes(-1), referrerHost: "example.com")
        ]);
        await dbContext.SaveChangesAsync();

        GetLinkStatsQueryHandler handler = CreateHandler(dbContext, CreateFixedTimeProvider(now));

        // Act
        GetLinkStatsResult result = await handler.Handle(
            new GetLinkStatsQuery(linkId),
            CancellationToken.None);

        // Assert
        var found = result.Should().BeOfType<GetLinkStatsResult.Found>().Subject;
        found.Stats.TopReferrers.Should().HaveCount(1);
        found.Stats.TopReferrers[0].Host.Should().Be("example.com");
    }

    [Fact]
    public async Task Handle_TopReferrers_OrderedByCountDescending()
    {
        // Arrange
        using TestAppDbContext dbContext = CreateDbContext();
        Guid linkId = Guid.NewGuid();
        await dbContext.Links.AddAsync(CreateLink(linkId));

        DateTimeOffset now = DateTimeOffset.UtcNow;
        // a.com: 3 visits, b.com: 5 visits, c.com: 1 visit → expected order: b.com, a.com, c.com
        List<Visit> visits = [
            CreateVisit(linkId, now, referrerHost: "a.com"),
            CreateVisit(linkId, now.AddMinutes(-1), referrerHost: "a.com"),
            CreateVisit(linkId, now.AddMinutes(-2), referrerHost: "a.com"),
            CreateVisit(linkId, now.AddMinutes(-3), referrerHost: "b.com"),
            CreateVisit(linkId, now.AddMinutes(-4), referrerHost: "b.com"),
            CreateVisit(linkId, now.AddMinutes(-5), referrerHost: "b.com"),
            CreateVisit(linkId, now.AddMinutes(-6), referrerHost: "b.com"),
            CreateVisit(linkId, now.AddMinutes(-7), referrerHost: "b.com"),
            CreateVisit(linkId, now.AddMinutes(-8), referrerHost: "c.com")
        ];
        await dbContext.Visits.AddRangeAsync(visits);
        await dbContext.SaveChangesAsync();

        GetLinkStatsQueryHandler handler = CreateHandler(dbContext, CreateFixedTimeProvider(now));

        // Act
        GetLinkStatsResult result = await handler.Handle(
            new GetLinkStatsQuery(linkId),
            CancellationToken.None);

        // Assert
        var found = result.Should().BeOfType<GetLinkStatsResult.Found>().Subject;
        found.Stats.TopReferrers.Should().HaveCount(3);
        found.Stats.TopReferrers[0].Host.Should().Be("b.com");
        found.Stats.TopReferrers[0].Count.Should().Be(5);
        found.Stats.TopReferrers[1].Host.Should().Be("a.com");
        found.Stats.TopReferrers[1].Count.Should().Be(3);
        found.Stats.TopReferrers[2].Host.Should().Be("c.com");
        found.Stats.TopReferrers[2].Count.Should().Be(1);
    }

    [Fact]
    public async Task Handle_TopReferrers_LimitedToTop10()
    {
        // Arrange
        using TestAppDbContext dbContext = CreateDbContext();
        Guid linkId = Guid.NewGuid();
        await dbContext.Links.AddAsync(CreateLink(linkId));

        DateTimeOffset now = DateTimeOffset.UtcNow;
        // 15 distinct referrer hosts, each with 1 visit
        List<Visit> visits = Enumerable.Range(1, 15)
            .Select(i => CreateVisit(linkId, now.AddMinutes(-i), referrerHost: $"ref{i}.com"))
            .ToList();
        await dbContext.Visits.AddRangeAsync(visits);
        await dbContext.SaveChangesAsync();

        GetLinkStatsQueryHandler handler = CreateHandler(dbContext, CreateFixedTimeProvider(now));

        // Act
        GetLinkStatsResult result = await handler.Handle(
            new GetLinkStatsQuery(linkId),
            CancellationToken.None);

        // Assert
        var found = result.Should().BeOfType<GetLinkStatsResult.Found>().Subject;
        found.Stats.TopReferrers.Should().HaveCount(10);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Isolation: stats are scoped to the queried link
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_StatsAreScopedToQueriedLink_OtherLinksVisitsExcluded()
    {
        // Arrange
        using TestAppDbContext dbContext = CreateDbContext();
        Guid linkIdA = Guid.NewGuid();
        Guid linkIdB = Guid.NewGuid();
        await dbContext.Links.AddRangeAsync([CreateLink(linkIdA, "aaaaaa"), CreateLink(linkIdB, "bbbbbb")]);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        await dbContext.Visits.AddRangeAsync([
            CreateVisit(linkIdA, now),
            CreateVisit(linkIdA, now.AddMinutes(-1)),
            CreateVisit(linkIdB, now.AddMinutes(-2)),
            CreateVisit(linkIdB, now.AddMinutes(-3)),
            CreateVisit(linkIdB, now.AddMinutes(-4))
        ]);
        await dbContext.SaveChangesAsync();

        GetLinkStatsQueryHandler handler = CreateHandler(dbContext, CreateFixedTimeProvider(now));

        // Act
        GetLinkStatsResult resultA = await handler.Handle(new GetLinkStatsQuery(linkIdA), CancellationToken.None);
        GetLinkStatsResult resultB = await handler.Handle(new GetLinkStatsQuery(linkIdB), CancellationToken.None);

        // Assert
        resultA.Should().BeOfType<GetLinkStatsResult.Found>()
            .Subject.Stats.TotalVisits.Should().Be(2);
        resultB.Should().BeOfType<GetLinkStatsResult.Found>()
            .Subject.Stats.TotalVisits.Should().Be(3);
    }
}
