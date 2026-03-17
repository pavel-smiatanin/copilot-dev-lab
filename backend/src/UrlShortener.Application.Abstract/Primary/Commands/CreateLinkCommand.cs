using MediatR;

namespace UrlShortener.Application.Abstract.Primary.Commands;

public sealed record CreateLinkCommand(
    string DestinationUrl,
    string? CustomAlias,
    DateTimeOffset? ExpiresAt,
    string? Password) : IRequest<CreateLinkResult>;
