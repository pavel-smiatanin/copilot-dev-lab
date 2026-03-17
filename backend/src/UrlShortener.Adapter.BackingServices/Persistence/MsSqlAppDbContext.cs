using Microsoft.EntityFrameworkCore;
using UrlShortener.Application.Abstract.Secondary;

namespace UrlShortener.Adapter.BackingServices.Persistence;

public class MsSqlAppDbContext : AppDbContext
{
    public MsSqlAppDbContext(DbContextOptions<MsSqlAppDbContext> options)
        : base(options)
    { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
