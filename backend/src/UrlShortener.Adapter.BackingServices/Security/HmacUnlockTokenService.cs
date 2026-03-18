using System.Security.Cryptography;
using System.Text;
using UrlShortener.Application.Abstract.Secondary;

namespace UrlShortener.Adapter.BackingServices.Security;

public sealed class HmacUnlockTokenService : IUnlockTokenService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(5);

    private readonly byte[] _secret;

    public HmacUnlockTokenService(byte[] secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        if (secret.Length == 0)
        {
            throw new ArgumentException("HMAC secret must not be empty.", nameof(secret));
        }

        _secret = secret;
    }

    public string Issue(string alias, DateTimeOffset issuedAt)
    {
        long expiryEpoch = (issuedAt + TokenLifetime).ToUnixTimeSeconds();
        string payload = BuildPayload(alias, expiryEpoch);

        string encodedPayload = Base64UrlEncode(payload);
        string signature = ComputeSignature(payload);

        return $"{encodedPayload}.{signature}";
    }

    public bool Validate(string alias, string token, DateTimeOffset now)
    {
        int dotIndex = token.IndexOf('.');
        if (dotIndex < 0)
        {
            return false;
        }

        string encodedPayload = token[..dotIndex];
        string signature = token[(dotIndex + 1)..];

        string payload;
        try
        {
            payload = Base64UrlDecode(encodedPayload);
        }
        catch (FormatException)
        {
            return false;
        }

        string expectedSignature = ComputeSignature(payload);
        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedSignature),
            Encoding.UTF8.GetBytes(signature)))
        {
            return false;
        }

        string[] parts = payload.Split(':');
        if (parts.Length != 2)
        {
            return false;
        }

        string tokenAlias = parts[0];
        if (!string.Equals(tokenAlias, alias, StringComparison.Ordinal))
        {
            return false;
        }

        if (!long.TryParse(parts[1], out long expiryEpoch))
        {
            return false;
        }

        DateTimeOffset expiry = DateTimeOffset.FromUnixTimeSeconds(expiryEpoch);
        return now < expiry;
    }

    private static string BuildPayload(string alias, long expiryEpoch)
        => $"{alias}:{expiryEpoch}";

    private string ComputeSignature(string payload)
    {
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
        byte[] hash = HMACSHA256.HashData(_secret, payloadBytes);
        return Convert.ToBase64String(hash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string Base64UrlEncode(string value)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string Base64UrlDecode(string value)
    {
        string padded = value
            .Replace('-', '+')
            .Replace('_', '/');

        padded = (padded.Length % 4) switch
        {
            2 => padded + "==",
            3 => padded + "=",
            _ => padded
        };

        return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
    }
}
