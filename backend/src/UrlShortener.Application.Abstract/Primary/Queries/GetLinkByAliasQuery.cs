using MediatR;

namespace UrlShortener.Application.Abstract.Primary.Queries;

public sealed record GetLinkByAliasQuery(string Alias) : IRequest<GetLinkByAliasResult>;
