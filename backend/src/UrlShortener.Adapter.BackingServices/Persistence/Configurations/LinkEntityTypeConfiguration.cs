using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UrlShortener.Application.Abstract.Model;

namespace UrlShortener.Adapter.BackingServices.Persistence.Configurations;

internal sealed class LinkEntityTypeConfiguration : IEntityTypeConfiguration<Link>
{
    public void Configure(EntityTypeBuilder<Link> builder)
    {
        builder.ToTable("links");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .HasColumnName("id");

        builder.Property(l => l.Alias)
            .HasColumnName("alias")
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(l => l.DestinationUrl)
            .HasColumnName("destination_url")
            .IsRequired()
            .HasMaxLength(2048);

        builder.Property(l => l.Title)
            .HasColumnName("title")
            .HasMaxLength(500);

        builder.Property(l => l.OgTitle)
            .HasColumnName("og_title")
            .HasMaxLength(500);

        builder.Property(l => l.OgImageUrl)
            .HasColumnName("og_image_url")
            .HasMaxLength(2048);

        builder.Property(l => l.FaviconUrl)
            .HasColumnName("favicon_url")
            .HasMaxLength(2048);

        builder.Property(l => l.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(100);

        builder.Property(l => l.ExpiresAt)
            .HasColumnName("expiry_at");

        builder.Property(l => l.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(l => l.RowVersion)
            .HasColumnName("row_version")
            .IsRowVersion();

        // Unique index on alias — case-insensitive via SQL_Latin1_General_CP1_CI_AS collation
        // set explicitly on the column during schema creation (see FluentMigrator migration).
        builder.HasIndex(l => l.Alias)
            .IsUnique()
            .HasDatabaseName("UX_links_alias");

        // Index for background cleanup queries filtering on expiry
        builder.HasIndex(l => l.ExpiresAt)
            .HasDatabaseName("IX_links_expiry_at");
    }
}
