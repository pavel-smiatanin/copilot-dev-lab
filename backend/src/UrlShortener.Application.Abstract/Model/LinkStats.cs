namespace UrlShortener.Application.Abstract.Model;

public sealed record LinkStats(
    long TotalVisits,
    long UniqueVisitors,
    IReadOnlyList<VisitsByDay> VisitsByDay,
    IReadOnlyList<ReferrerCount> TopReferrers);
