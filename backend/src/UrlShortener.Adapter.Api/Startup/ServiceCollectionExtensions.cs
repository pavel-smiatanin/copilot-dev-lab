using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using UrlShortener.Adapter.BackingServices.Metadata;
using UrlShortener.Adapter.BackingServices.Persistence;
using UrlShortener.Adapter.BackingServices.Security;
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

        services.AddTransient<SsrfProtectionHandler>();

        services.AddHttpClient<LinkMetadataFetcher>(client =>
            {
                // Outer guard � the fetcher itself enforces a 5-second CancellationTokenSource timeout
                client.Timeout = TimeSpan.FromSeconds(6);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("UrlShortener-MetadataFetcher/1.0");
            })
            .AddHttpMessageHandler<SsrfProtectionHandler>();

        services.AddTransient<ILinkMetadataFetcher>(
            sp => sp.GetRequiredService<LinkMetadataFetcher>());

        string hmacSecret = configuration["UnlockToken:HmacSecret"]
            ?? throw new InvalidOperationException(
                "Configuration value 'UnlockToken:HmacSecret' is not configured.");

        services.AddSingleton<IUnlockTokenService>(
            new HmacUnlockTokenService(Encoding.UTF8.GetBytes(hmacSecret)));

        return services;
    }
}
