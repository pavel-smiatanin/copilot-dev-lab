using System.Net;
using UrlShortener.Shared.Url;

namespace UrlShortener.Shared.Tests.Url;

public sealed class PrivateNetworkGuardTests
{
    // ---------------------------------------------------------------------------
    // IsPrivateOrReserved — IPv4 loopback
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.0.0.2")]
    [InlineData("127.255.255.255")]
    public void IsPrivateOrReserved_WhenIpv4Loopback_ReturnsTrue(string ipString)
    {
        IPAddress ip = IPAddress.Parse(ipString);

        bool result = PrivateNetworkGuard.IsPrivateOrReserved(ip);

        result.Should().BeTrue();
    }

    // ---------------------------------------------------------------------------
    // IsPrivateOrReserved — IPv4 RFC 1918 private ranges
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("10.0.0.1")]
    [InlineData("10.255.255.255")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.0.1")]
    [InlineData("192.168.255.255")]
    public void IsPrivateOrReserved_WhenRfc1918Address_ReturnsTrue(string ipString)
    {
        IPAddress ip = IPAddress.Parse(ipString);

        bool result = PrivateNetworkGuard.IsPrivateOrReserved(ip);

        result.Should().BeTrue();
    }

    // ---------------------------------------------------------------------------
    // IsPrivateOrReserved — IPv4 link-local
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("169.254.0.1")]
    [InlineData("169.254.169.254")] // AWS metadata endpoint
    [InlineData("169.254.255.255")]
    public void IsPrivateOrReserved_WhenIpv4LinkLocal_ReturnsTrue(string ipString)
    {
        IPAddress ip = IPAddress.Parse(ipString);

        bool result = PrivateNetworkGuard.IsPrivateOrReserved(ip);

        result.Should().BeTrue();
    }

    // ---------------------------------------------------------------------------
    // IsPrivateOrReserved — IPv4 other reserved ranges
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("0.255.255.255")]
    [InlineData("100.64.0.1")]    // CGNAT / shared address space
    [InlineData("100.127.255.255")]
    [InlineData("240.0.0.1")]     // Reserved
    [InlineData("255.255.255.255")] // Broadcast
    public void IsPrivateOrReserved_WhenIpv4OtherReserved_ReturnsTrue(string ipString)
    {
        IPAddress ip = IPAddress.Parse(ipString);

        bool result = PrivateNetworkGuard.IsPrivateOrReserved(ip);

        result.Should().BeTrue();
    }

    // ---------------------------------------------------------------------------
    // IsPrivateOrReserved — IPv4 public addresses (must NOT be blocked)
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("172.15.255.255")]  // just below 172.16.0.0/12
    [InlineData("172.32.0.0")]      // just above 172.31.255.255
    [InlineData("100.128.0.0")]     // just above CGNAT range
    [InlineData("100.63.255.255")]  // just below CGNAT range
    [InlineData("203.0.114.1")]
    public void IsPrivateOrReserved_WhenIpv4PublicAddress_ReturnsFalse(string ipString)
    {
        IPAddress ip = IPAddress.Parse(ipString);

        bool result = PrivateNetworkGuard.IsPrivateOrReserved(ip);

        result.Should().BeFalse();
    }

    // ---------------------------------------------------------------------------
    // IsPrivateOrReserved — IPv6 loopback
    // ---------------------------------------------------------------------------

    [Fact]
    public void IsPrivateOrReserved_WhenIpv6Loopback_ReturnsTrue()
    {
        IPAddress ip = IPAddress.IPv6Loopback; // ::1

        bool result = PrivateNetworkGuard.IsPrivateOrReserved(ip);

        result.Should().BeTrue();
    }

    // ---------------------------------------------------------------------------
    // IsPrivateOrReserved — IPv6 link-local
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("fe80::1")]
    [InlineData("fe80::1%eth0")]
    [InlineData("febf::1")]
    public void IsPrivateOrReserved_WhenIpv6LinkLocal_ReturnsTrue(string ipString)
    {
        IPAddress ip = IPAddress.Parse(ipString);

        bool result = PrivateNetworkGuard.IsPrivateOrReserved(ip);

        result.Should().BeTrue();
    }

    // ---------------------------------------------------------------------------
    // IsPrivateOrReserved — IPv6 unique local (fc00::/7)
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("fc00::1")]
    [InlineData("fd00::1")]
    [InlineData("fdff:ffff:ffff:ffff:ffff:ffff:ffff:ffff")]
    public void IsPrivateOrReserved_WhenIpv6UniqueLocal_ReturnsTrue(string ipString)
    {
        IPAddress ip = IPAddress.Parse(ipString);

        bool result = PrivateNetworkGuard.IsPrivateOrReserved(ip);

        result.Should().BeTrue();
    }

    // ---------------------------------------------------------------------------
    // IsPrivateOrReserved — IPv6 unspecified
    // ---------------------------------------------------------------------------

    [Fact]
    public void IsPrivateOrReserved_WhenIpv6Unspecified_ReturnsTrue()
    {
        IPAddress ip = IPAddress.IPv6Any; // ::

        bool result = PrivateNetworkGuard.IsPrivateOrReserved(ip);

        result.Should().BeTrue();
    }

    // ---------------------------------------------------------------------------
    // IsPrivateOrReserved — IPv4-mapped IPv6 addresses
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("::ffff:10.0.0.1")]
    [InlineData("::ffff:192.168.1.1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:169.254.169.254")]
    public void IsPrivateOrReserved_WhenIpv4MappedIpv6PrivateAddress_ReturnsTrue(string ipString)
    {
        IPAddress ip = IPAddress.Parse(ipString);

        bool result = PrivateNetworkGuard.IsPrivateOrReserved(ip);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsPrivateOrReserved_WhenIpv4MappedIpv6PublicAddress_ReturnsFalse()
    {
        IPAddress ip = IPAddress.Parse("::ffff:8.8.8.8");

        bool result = PrivateNetworkGuard.IsPrivateOrReserved(ip);

        result.Should().BeFalse();
    }

    // ---------------------------------------------------------------------------
    // IsHostAllowedAsync — IP address literals (no real DNS needed)
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("172.16.0.1")]
    [InlineData("169.254.169.254")]
    public async Task IsHostAllowedAsync_WhenPrivateIpLiteral_ReturnsFalse(string host)
    {
        bool result = await PrivateNetworkGuard.IsHostAllowedAsync(host, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    public async Task IsHostAllowedAsync_WhenPublicIpLiteral_ReturnsTrue(string host)
    {
        bool result = await PrivateNetworkGuard.IsHostAllowedAsync(host, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("::1")]
    [InlineData("fc00::1")]
    [InlineData("fd12::1")]
    [InlineData("fe80::1")]
    public async Task IsHostAllowedAsync_WhenPrivateIpv6Literal_ReturnsFalse(string host)
    {
        bool result = await PrivateNetworkGuard.IsHostAllowedAsync(host, CancellationToken.None);

        result.Should().BeFalse();
    }

    // ---------------------------------------------------------------------------
    // IsHostAllowedAsync — localhost hostname
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task IsHostAllowedAsync_WhenLocalhostHostname_ReturnsFalse()
    {
        bool result = await PrivateNetworkGuard.IsHostAllowedAsync("localhost", CancellationToken.None);

        result.Should().BeFalse();
    }

    // ---------------------------------------------------------------------------
    // IsHostAllowedAsync — unresolvable hostname
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task IsHostAllowedAsync_WhenUnresolvableHostname_ReturnsFalse()
    {
        bool result = await PrivateNetworkGuard.IsHostAllowedAsync(
            "this-hostname-does-not-exist.invalid",
            CancellationToken.None);

        result.Should().BeFalse();
    }
}
