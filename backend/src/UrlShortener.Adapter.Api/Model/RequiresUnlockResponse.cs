namespace UrlShortener.Adapter.Api.Model;

/// <summary>
/// Returned with 200 when a redirect target requires an unlock password.
/// The frontend SPA uses this signal to display the unlock form.
/// </summary>
public sealed record RequiresUnlockResponse(bool RequiresUnlock, string Alias);
