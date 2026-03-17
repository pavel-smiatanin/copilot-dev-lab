using System.Security.Cryptography;

namespace UrlShortener.Shared.ShortId;

public static class ShortIdGenerator
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    private const int DefaultLength = 6;

    // Rejection-sampling threshold: discard bytes >= threshold to avoid modulo bias.
    // 256 / 62 = 4 remainder 8 → threshold = 4 * 62 = 248
    private static readonly int Threshold = (256 / Alphabet.Length) * Alphabet.Length;

    public static string Generate(int length = DefaultLength)
    {
        char[] chars = new char[length];
        Span<byte> buffer = stackalloc byte[length * 2];
        int filled = 0;

        while (filled < length)
        {
            RandomNumberGenerator.Fill(buffer);
            foreach (byte b in buffer)
            {
                if (filled >= length)
                {
                    break;
                }

                if (b < Threshold)
                {
                    chars[filled++] = Alphabet[b % Alphabet.Length];
                }
            }
        }

        return new string(chars);
    }
}
