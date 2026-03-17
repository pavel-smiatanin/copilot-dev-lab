using Microsoft.EntityFrameworkCore;
using UrlShortener.Application.Abstract.Model;

namespace UrlShortener.Application.Abstract.Secondary;

public abstract class AppDbContext : DbContext
{
    // Non-generic DbContextOptions allows concrete subclass to use DbContextOptions<TConcreteContext>
    protected AppDbContext(DbContextOptions options)
        : base(options)
    { }

    public DbSet<Link> Links { get; set; } = null!;

    public DbSet<Visit> Visits { get; set; } = null!;
}
