namespace UrlShortener.Application.Abstract.Primary.Commands;

public abstract record UnlockLinkResult
{
    public sealed record Success(string Token) : UnlockLinkResult;

    public sealed record NotFound() : UnlockLinkResult;

    public sealed record InvalidPassword() : UnlockLinkResult;
}
