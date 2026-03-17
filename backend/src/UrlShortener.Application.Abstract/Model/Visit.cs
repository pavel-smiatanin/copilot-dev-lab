namespace UrlShortener.Application.Abstract.Model;

public sealed class Visit
{
    public Guid Id { get; set; }

    public Guid LinkId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public string HashedIp { get; set; }

    public string? ReferrerHost { get; set; }

    public string? UserAgent { get; set; }

    public string? CountryCode { get; set; }

    // EF Core navigation property — populated by ORM after materialization
    public Link Link { get; set; } = null!;
}
