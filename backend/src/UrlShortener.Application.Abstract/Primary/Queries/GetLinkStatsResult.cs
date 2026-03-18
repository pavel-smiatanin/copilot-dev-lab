using UrlShortener.Application.Abstract.Model;

namespace UrlShortener.Application.Abstract.Primary.Queries;

public abstract record GetLinkStatsResult
{
    public sealed record NotFound : GetLinkStatsResult;

    public sealed record Found(LinkStats Stats) : GetLinkStatsResult;
}
