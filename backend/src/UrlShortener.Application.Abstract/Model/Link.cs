using System.ComponentModel.DataAnnotations;

namespace UrlShortener.Application.Abstract.Model;

public sealed class Link
{
    public Guid Id { get; set; }

    public string Alias { get; set; }

    public string DestinationUrl { get; set; }

    public string? Title { get; set; }

    public string? OgTitle { get; set; }

    public string? OgImageUrl { get; set; }

    public string? FaviconUrl { get; set; }

    public string? PasswordHash { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public ICollection<Visit> Visits { get; set; } = [];
}
