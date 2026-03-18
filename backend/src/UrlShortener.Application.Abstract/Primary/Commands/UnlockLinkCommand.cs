using MediatR;

namespace UrlShortener.Application.Abstract.Primary.Commands;

public sealed record UnlockLinkCommand(
    string Alias,
    string Password) : IRequest<UnlockLinkResult>;
