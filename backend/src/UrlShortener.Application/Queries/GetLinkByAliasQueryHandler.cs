using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using UrlShortener.Application.Abstract.Primary.Queries;
using UrlShortener.Application.Abstract.Secondary;
using ILogger = Serilog.ILogger;

namespace UrlShortener.Application.Queries;

public sealed class GetLinkByAliasQueryHandler : IRequestHandler<GetLinkByAliasQuery, GetLinkByAliasResult>
{
    private static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromHours(24);

    private readonly AppDbContext _dbContext;
    private readonly IDistributedCache _cache;
    private readonly IUnlockTokenService _tokenService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    public GetLinkByAliasQueryHandler(
        AppDbContext dbContext,
        IDistributedCache cache,
        IUnlockTokenService tokenService,
        TimeProvider timeProvider,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(tokenService);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _dbContext = dbContext;
        _cache = cache;
        _tokenService = tokenService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<GetLinkByAliasResult> Handle(
        GetLinkByAliasQuery request,
        CancellationToken cancellationToken)
    {
        string cacheKey = BuildCacheKey(request.Alias);

        CachedLinkData? cached = await TryGetFromCacheAsync(cacheKey, cancellationToken);

        if (cached is not null)
        {
            _logger.Debug("Cache hit for alias {Alias}", request.Alias);
            return ResolveFromCachedData(request.Alias, request.Token, cached);
        }

        Abstract.Model.Link? link = await _dbContext.Links
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Alias == request.Alias, cancellationToken);

        if (link is null)
        {
            return new GetLinkByAliasResult.NotFound();
        }

        await TryPopulateCacheAsync(cacheKey, link, cancellationToken);

        return ResolveFromLink(request.Alias, request.Token, link);
    }

    private GetLinkByAliasResult ResolveFromCachedData(string alias, string? token, CachedLinkData cached)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();

        if (cached.ExpiresAt.HasValue && cached.ExpiresAt.Value <= now)
        {
            return new GetLinkByAliasResult.NotFound();
        }

        if (cached.HasPassword)
        {
            if (token is not null && _tokenService.Validate(alias, token, now))
            {
                return new GetLinkByAliasResult.Redirect(cached.DestinationUrl);
            }

            return new GetLinkByAliasResult.RequiresUnlock();
        }

        return new GetLinkByAliasResult.Redirect(cached.DestinationUrl);
    }

    private GetLinkByAliasResult ResolveFromLink(string alias, string? token, Abstract.Model.Link link)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();

        if (link.ExpiresAt.HasValue && link.ExpiresAt.Value <= now)
        {
            return new GetLinkByAliasResult.NotFound();
        }

        if (link.PasswordHash is not null)
        {
            if (token is not null && _tokenService.Validate(alias, token, now))
            {
                return new GetLinkByAliasResult.Redirect(link.DestinationUrl);
            }

            return new GetLinkByAliasResult.RequiresUnlock();
        }

        return new GetLinkByAliasResult.Redirect(link.DestinationUrl);
    }

    private async Task<CachedLinkData?> TryGetFromCacheAsync(
        string cacheKey,
        CancellationToken cancellationToken)
    {
        try
        {
            string? json = await _cache.GetStringAsync(cacheKey, cancellationToken);

            if (json is null)
            {
                return null;
            }

            return JsonSerializer.Deserialize<CachedLinkData>(json);
        }
        catch (Exception ex)
        {
            _logger.Warning(
                ex,
                "Redis unavailable during cache read; falling back to DB. Key={CacheKey}",
                cacheKey);
            return null;
        }
    }

    private async Task TryPopulateCacheAsync(
        string cacheKey,
        Abstract.Model.Link link,
        CancellationToken cancellationToken)
    {
        try
        {
            DateTimeOffset now = _timeProvider.GetUtcNow();

            TimeSpan ttl = link.ExpiresAt.HasValue
                ? TimeSpan.FromTicks(Math.Min((link.ExpiresAt.Value - now).Ticks, DefaultCacheTtl.Ticks))
                : DefaultCacheTtl;

            // Do not cache already-expired links
            if (ttl <= TimeSpan.Zero)
            {
                return;
            }

            CachedLinkData data = new(
                link.Id,
                link.DestinationUrl,
                link.ExpiresAt,
                link.PasswordHash is not null);

            string json = JsonSerializer.Serialize(data);

            await _cache.SetStringAsync(
                cacheKey,
                json,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Warning(
                ex,
                "Redis unavailable during cache write; alias resolution continues from DB. Key={CacheKey}",
                cacheKey);
        }
    }

    private static string BuildCacheKey(string alias) => $"alias:{alias.ToLowerInvariant()}";
}
