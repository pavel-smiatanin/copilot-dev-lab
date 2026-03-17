using FluentMigrator;
using System.Data;

namespace UrlShortener.Database.Migrations.Migrations;

[Migration(1, "Create initial schema with links and visits tables")]
public sealed class M001_CreateInitialSchema : Migration
{
    public override void Up()
    {
        Create.Table("links")
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable()
            .WithColumn("alias").AsString(50).NotNullable()
            .WithColumn("destination_url").AsString(2048).NotNullable()
            .WithColumn("title").AsString(500).Nullable()
            .WithColumn("og_title").AsString(500).Nullable()
            .WithColumn("og_image_url").AsString(2048).Nullable()
            .WithColumn("favicon_url").AsString(2048).Nullable()
            .WithColumn("password_hash").AsString(100).Nullable()
            .WithColumn("expiry_at").AsDateTimeOffset().Nullable()
            .WithColumn("created_at").AsDateTimeOffset().NotNullable()
            // rowversion is auto-generated and auto-updated by SQL Server
            .WithColumn("row_version").AsCustom("rowversion").NotNullable();

        // Unique index on alias with explicit case-insensitive collation.
        // The collation is set on the column expression so the constraint is
        // CI regardless of the database's default collation settings.
        Create.Index("UX_links_alias")
            .OnTable("links")
            .OnColumn("alias").Ascending()
            .WithOptions().Unique()
            .WithOptions().NonClustered();

        // Index on expiry_at for efficient expired-link queries in background cleanup
        Create.Index("IX_links_expiry_at")
            .OnTable("links")
            .OnColumn("expiry_at").Ascending();

        Create.Table("visits")
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable()
            .WithColumn("link_id").AsGuid().NotNullable()
            .WithColumn("occurred_at").AsDateTimeOffset().NotNullable()
            .WithColumn("hashed_ip").AsString(64).NotNullable()
            .WithColumn("referrer_host").AsString(255).Nullable()
            .WithColumn("user_agent").AsString(512).Nullable()
            .WithColumn("country_code").AsFixedLengthString(2).Nullable();

        // FK with cascade delete so visits are removed when the link is deleted
        Create.ForeignKey("FK_visits_link_id")
            .FromTable("visits").ForeignColumn("link_id")
            .ToTable("links").PrimaryColumn("id")
            .OnDeleteOrUpdate(Rule.Cascade);

        // Composite index for analytics queries: link_id ascending, occurred_at descending
        Create.Index("IX_visits_link_id_occurred_at")
            .OnTable("visits")
            .OnColumn("link_id").Ascending()
            .OnColumn("occurred_at").Descending();
    }

    public override void Down()
    {
        Delete.Table("visits");
        Delete.Table("links");
    }
}
