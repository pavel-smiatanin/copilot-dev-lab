using System.Net;
using System.Net.Sockets;

namespace UrlShortener.Shared.Url;

/// <summary>
/// Guards against SSRF attacks by detecting whether an IP address or hostname
/// resolves to a private, loopback, or link-local network range.
/// </summary>
public static class PrivateNetworkGuard
{
    /// <summary>
    /// Returns <c>true</c> if the given IP address falls within a private or reserved range
    /// (RFC 1918, loopback, link-local, CGNAT, or otherwise non-routable).
    /// </summary>
    public static bool IsPrivateOrReserved(IPAddress ip)
    {
        // Normalize IPv4-mapped IPv6 addresses (::ffff:x.x.x.x) to plain IPv4
        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        // Loopback: 127.0.0.0/8 (IPv4) or ::1 (IPv6)
        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        return ip.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsPrivateIpv4(ip),
            AddressFamily.InterNetworkV6 => IsPrivateIpv6(ip),
            _ => false,
        };
    }

    /// <summary>
    /// Resolves <paramref name="host"/> via DNS and returns <c>true</c> only when
    /// every resolved address is a publicly routable address (none are private or reserved).
    /// Returns <c>false</c> on DNS failure (fail-closed behaviour).
    /// </summary>
    public static async Task<bool> IsHostAllowedAsync(string host, CancellationToken cancellationToken)
    {
        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
        }
        catch (Exception)
        {
            // DNS resolution failure — fail closed to prevent enumeration of internal hosts
            return false;
        }

        if (addresses.Length == 0)
        {
            return false;
        }

        // Block if ANY resolved address is private or reserved (prevents split DNS attacks)
        return !Array.Exists(addresses, IsPrivateOrReserved);
    }

    private static bool IsPrivateIpv4(IPAddress ip)
    {
        byte[] b = ip.GetAddressBytes();

        // 0.0.0.0/8 — "This" network (RFC 1122)
        if (b[0] == 0) return true;

        // 10.0.0.0/8 — RFC 1918
        if (b[0] == 10) return true;

        // 100.64.0.0/10 — Shared address space / CGNAT (RFC 6598)
        if (b[0] == 100 && b[1] >= 64 && b[1] <= 127) return true;

        // 169.254.0.0/16 — Link-local (RFC 3927)
        if (b[0] == 169 && b[1] == 254) return true;

        // 172.16.0.0/12 — RFC 1918
        if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;

        // 192.168.0.0/16 — RFC 1918
        if (b[0] == 192 && b[1] == 168) return true;

        // 240.0.0.0/4 — Reserved (RFC 1112)
        if (b[0] >= 240) return true;

        return false;
    }

    private static bool IsPrivateIpv6(IPAddress ip)
    {
        // Unspecified — ::
        if (ip.Equals(IPAddress.IPv6Any)) return true;

        byte[] b = ip.GetAddressBytes();

        // Link-local — fe80::/10
        if (b[0] == 0xfe && (b[1] & 0xc0) == 0x80) return true;

        // Unique local — fc00::/7 (includes fc00:: and fd00::)
        if ((b[0] & 0xfe) == 0xfc) return true;

        return false;
    }
}
