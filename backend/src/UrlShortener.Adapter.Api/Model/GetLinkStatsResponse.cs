namespace UrlShortener.Adapter.Api.Model;

public sealed record GetLinkStatsResponse(
    long TotalVisits,
    long UniqueVisitors,
    IReadOnlyList<GetLinkStatsResponse.VisitsByDayEntry> VisitsByDay,
    IReadOnlyList<GetLinkStatsResponse.ReferrerEntry> TopReferrers)
{
    public sealed record VisitsByDayEntry(DateOnly Date, int Count);

    public sealed record ReferrerEntry(string Host, int Count);
}
