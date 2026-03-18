using MediatR;
using Microsoft.EntityFrameworkCore;
using Serilog;
using UrlShortener.Application.Abstract.Primary.Commands;
using UrlShortener.Application.Abstract.Secondary;

namespace UrlShortener.Application.Commands;

public sealed class UnlockLinkCommandHandler : IRequestHandler<UnlockLinkCommand, UnlockLinkResult>
{
    private readonly AppDbContext _dbContext;
    private readonly IUnlockTokenService _tokenService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    public UnlockLinkCommandHandler(
        AppDbContext dbContext,
        IUnlockTokenService tokenService,
        TimeProvider timeProvider,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(tokenService);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _dbContext = dbContext;
        _tokenService = tokenService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<UnlockLinkResult> Handle(
        UnlockLinkCommand request,
        CancellationToken cancellationToken)
    {
        var link = await _dbContext.Links
            .AsNoTracking()
            .Where(l => l.Alias == request.Alias)
            .Select(l => new { l.PasswordHash })
            .FirstOrDefaultAsync(cancellationToken);

        if (link is null)
        {
            return new UnlockLinkResult.NotFound();
        }

        if (link.PasswordHash is null || !BCrypt.Net.BCrypt.Verify(request.Password, link.PasswordHash))
        {
            _logger.Warning("Invalid password attempt for alias {Alias}", request.Alias);
            return new UnlockLinkResult.InvalidPassword();
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        string token = _tokenService.Issue(request.Alias, now);

        _logger.Information("Unlock token issued for alias {Alias}", request.Alias);

        return new UnlockLinkResult.Success(token);
    }
}
