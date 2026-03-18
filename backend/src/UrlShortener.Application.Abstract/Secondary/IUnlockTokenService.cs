namespace UrlShortener.Application.Abstract.Secondary;

public interface IUnlockTokenService
{
    string Issue(string alias, DateTimeOffset issuedAt);

    bool Validate(string alias, string token, DateTimeOffset now);
}
