using MediatR;
using Microsoft.EntityFrameworkCore;
using Serilog;
using UrlShortener.Application.Abstract.Model;
using UrlShortener.Application.Abstract.Primary.Commands;
using UrlShortener.Application.Abstract.Primary.Exceptions;
using UrlShortener.Application.Abstract.Secondary;
using UrlShortener.Shared.ShortId;

namespace UrlShortener.Application.Commands;

public sealed class CreateLinkCommandHandler : IRequestHandler<CreateLinkCommand, CreateLinkResult>
{
    private const int MaxCollisionRetries = 5;
    private const int SuggestionCount = 3;

    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILinkMetadataFetcher _metadataFetcher;
    private readonly ILogger _logger;

    public CreateLinkCommandHandler(
        AppDbContext dbContext,
        TimeProvider timeProvider,
        ILinkMetadataFetcher metadataFetcher,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(metadataFetcher);
        ArgumentNullException.ThrowIfNull(logger);
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _metadataFetcher = metadataFetcher;
        _logger = logger;
    }

    public async Task<CreateLinkResult> Handle(CreateLinkCommand request, CancellationToken cancellationToken)
    {
        string alias = request.CustomAlias is not null
            ? await ResolveCustomAliasAsync(request.CustomAlias, cancellationToken)
            : await GenerateUniqueAliasAsync(cancellationToken);

        string? passwordHash = request.Password is not null
            ? BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12)
            : null;

        LinkMetadata? metadata = await _metadataFetcher.FetchAsync(request.DestinationUrl, cancellationToken);

        Link link = new()
        {
            Id = Guid.NewGuid(),
            Alias = alias,
            DestinationUrl = request.DestinationUrl,
            Title = metadata?.Title,
            OgTitle = metadata?.OgTitle,
            OgImageUrl = metadata?.OgImageUrl,
            FaviconUrl = metadata?.FaviconUrl,
            PasswordHash = passwordHash,
            ExpiresAt = request.ExpiresAt,
            CreatedAt = _timeProvider.GetUtcNow(),
        };

        _dbContext.Links.Add(link);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.Information("Link created {LinkId} Alias={Alias}", link.Id, link.Alias);

        return new CreateLinkResult(
            link.Id,
            link.Alias,
            link.DestinationUrl,
            link.ExpiresAt,
            link.CreatedAt,
            link.PasswordHash is not null);
    }

    private async Task<string> ResolveCustomAliasAsync(string customAlias, CancellationToken cancellationToken)
    {
        bool aliasExists = await _dbContext.Links
            .AnyAsync(l => l.Alias == customAlias, cancellationToken);

        if (!aliasExists)
        {
            return customAlias;
        }

        List<string> suggestions = Enumerable
            .Range(0, SuggestionCount)
            .Select(_ => ShortIdGenerator.Generate())
            .ToList();

        throw new AliasConflictException(customAlias, suggestions);
    }

    private async Task<string> GenerateUniqueAliasAsync(CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < MaxCollisionRetries; attempt++)
        {
            string candidate = ShortIdGenerator.Generate();
            bool exists = await _dbContext.Links
                .AnyAsync(l => l.Alias == candidate, cancellationToken);

            if (!exists)
            {
                return candidate;
            }

            _logger.Warning(
                "Auto-generated alias collision on attempt {Attempt}, Alias={Alias}",
                attempt + 1,
                candidate);
        }

        throw new InvalidOperationException(
            $"Failed to generate a unique alias after {MaxCollisionRetries} attempts.");
    }
}
