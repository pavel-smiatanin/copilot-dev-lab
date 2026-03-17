using UrlShortener.Shared.Url;

namespace UrlShortener.Adapter.BackingServices.Metadata;

/// <summary>
/// DelegatingHandler that blocks HTTP requests targeting private, loopback, or link-local IP
/// addresses (SSRF protection). Resolves the destination hostname before forwarding the request.
/// </summary>
public sealed class SsrfProtectionHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        string host = request.RequestUri!.Host;
        bool allowed = await PrivateNetworkGuard.IsHostAllowedAsync(host, cancellationToken);

        if (!allowed)
        {
            throw new InvalidOperationException(
                $"Metadata fetch blocked: host '{host}' resolves to a private or reserved IP address.");
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
