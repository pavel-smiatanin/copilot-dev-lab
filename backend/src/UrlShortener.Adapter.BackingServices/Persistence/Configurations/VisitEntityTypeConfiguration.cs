using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UrlShortener.Application.Abstract.Model;

namespace UrlShortener.Adapter.BackingServices.Persistence.Configurations;

internal sealed class VisitEntityTypeConfiguration : IEntityTypeConfiguration<Visit>
{
    public void Configure(EntityTypeBuilder<Visit> builder)
    {
        builder.ToTable("visits");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id)
            .HasColumnName("id");

        builder.Property(v => v.LinkId)
            .HasColumnName("link_id");

        builder.Property(v => v.OccurredAt)
            .HasColumnName("occurred_at");

        builder.Property(v => v.HashedIp)
            .HasColumnName("hashed_ip")
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(v => v.ReferrerHost)
            .HasColumnName("referrer_host")
            .HasMaxLength(255);

        builder.Property(v => v.UserAgent)
            .HasColumnName("user_agent")
            .HasMaxLength(512);

        builder.Property(v => v.CountryCode)
            .HasColumnName("country_code")
            .HasMaxLength(2);

        builder.HasOne(v => v.Link)
            .WithMany(l => l.Visits)
            .HasForeignKey(v => v.LinkId)
            .HasConstraintName("FK_visits_link_id")
            .OnDelete(DeleteBehavior.Cascade);

        // Composite index for analytics: link_id + occurred_at DESC
        builder.HasIndex(v => new { v.LinkId, v.OccurredAt })
            .HasDatabaseName("IX_visits_link_id_occurred_at")
            .IsDescending(false, true);
    }
}
