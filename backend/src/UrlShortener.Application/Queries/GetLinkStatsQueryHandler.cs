using MediatR;
using Microsoft.EntityFrameworkCore;
using UrlShortener.Application.Abstract.Model;
using UrlShortener.Application.Abstract.Primary.Queries;
using UrlShortener.Application.Abstract.Secondary;
using ILogger = Serilog.ILogger;

namespace UrlShortener.Application.Queries;

public sealed class GetLinkStatsQueryHandler : IRequestHandler<GetLinkStatsQuery, GetLinkStatsResult>
{
    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    public GetLinkStatsQueryHandler(AppDbContext dbContext, TimeProvider timeProvider, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<GetLinkStatsResult> Handle(GetLinkStatsQuery request, CancellationToken cancellationToken)
    {
        bool linkExists = await _dbContext.Links
            .AsNoTracking()
            .AnyAsync(l => l.Id == request.LinkId, cancellationToken);

        if (!linkExists)
        {
            return new GetLinkStatsResult.NotFound();
        }

        DateTimeOffset cutoff = _timeProvider.GetUtcNow().AddDays(-30);

        long totalVisits = await _dbContext.Visits
            .Where(v => v.LinkId == request.LinkId)
            .LongCountAsync(cancellationToken);

        long uniqueVisitors = await _dbContext.Visits
            .Where(v => v.LinkId == request.LinkId)
            .Select(v => v.HashedIp)
            .Distinct()
            .LongCountAsync(cancellationToken);

        // Materialize first to avoid mixing EF Core translation with client-side DateOnly construction
        var rawDailyVisits = await _dbContext.Visits
            .Where(v => v.LinkId == request.LinkId && v.OccurredAt >= cutoff)
            .GroupBy(v => new { v.OccurredAt.Year, v.OccurredAt.Month, v.OccurredAt.Day })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Count = g.Count() })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ThenBy(x => x.Day)
            .ToListAsync(cancellationToken);

        List<VisitsByDay> visitsByDay = rawDailyVisits
            .Select(x => new VisitsByDay(new DateOnly(x.Year, x.Month, x.Day), x.Count))
            .ToList();

        var rawTopReferrers = await _dbContext.Visits
            .Where(v => v.LinkId == request.LinkId && v.ReferrerHost != null)
            // ReferrerHost is guaranteed non-null by the Where filter above
            .GroupBy(v => v.ReferrerHost!)
            .Select(g => new { Host = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync(cancellationToken);

        List<ReferrerCount> topReferrers = rawTopReferrers
            .Select(x => new ReferrerCount(x.Host, x.Count))
            .ToList();

        LinkStats stats = new(totalVisits, uniqueVisitors, visitsByDay, topReferrers);

        _logger.Debug(
            "Stats fetched for link {LinkId}: {TotalVisits} total visits, {UniqueVisitors} unique visitors",
            request.LinkId,
            totalVisits,
            uniqueVisitors);

        return new GetLinkStatsResult.Found(stats);
    }
}
