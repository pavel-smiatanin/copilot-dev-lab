namespace UrlShortener.Application.Abstract.Primary.Queries;

public abstract record GetLinkByAliasResult
{
    public sealed record NotFound : GetLinkByAliasResult;

    public sealed record Redirect(string DestinationUrl) : GetLinkByAliasResult;

    public sealed record RequiresUnlock : GetLinkByAliasResult;
}
