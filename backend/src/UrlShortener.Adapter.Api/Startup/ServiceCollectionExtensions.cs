using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using UrlShortener.Adapter.BackingServices.Persistence;
using UrlShortener.Application;
using UrlShortener.Application.Abstract.Secondary;
using UrlShortener.Shared.MediatR;

namespace UrlShortener.Adapter.Api.Startup;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        services
            .AddMediatR(cfg =>
                {
                    cfg.RegisterServicesFromAssembly(typeof(ApplicationAssemblyMarker).Assembly);
                    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
                })
            .AddValidatorsFromAssembly(typeof(ApplicationAssemblyMarker).Assembly);

        return services;
    }

    public static IServiceCollection AddBackingServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string sqlConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is not configured.");

        services.AddDbContext<AppDbContext, MsSqlAppDbContext>(options =>
            options.UseSqlServer(sqlConnectionString));

        string redisConnectionString = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException(
                "Connection string 'Redis' is not configured.");

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = "UrlShortener:";
        });

        return services;
    }
}
