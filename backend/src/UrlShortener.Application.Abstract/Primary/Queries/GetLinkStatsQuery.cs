using MediatR;

namespace UrlShortener.Application.Abstract.Primary.Queries;

public sealed record GetLinkStatsQuery(Guid LinkId) : IRequest<GetLinkStatsResult>;
