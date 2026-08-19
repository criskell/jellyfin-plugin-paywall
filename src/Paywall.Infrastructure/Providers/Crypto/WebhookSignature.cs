using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Paywall.Infrastructure.Providers.Crypto;

internal static class WebhookSignature
{
    public static bool MatchesSha256(string body, string secret, string? received) =>
        MatchesDigest(HMACSHA256.HashData(Utf8(secret), Utf8(body)), received);

    public static bool MatchesSha512(string body, string secret, string? received) =>
        MatchesDigest(HMACSHA512.HashData(Utf8(secret), Utf8(body)), received);

    public static string Sha256Hex(string message, string secret) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(Utf8(secret), Utf8(message)));

    private static bool MatchesDigest(byte[] expected, string? received)
    {
        if (received is null)
        {
            return false;
        }

        Span<byte> offered = stackalloc byte[expected.Length];

        return TryDecodeHex(WithoutAlgorithmPrefix(received), offered)
               && CryptographicOperations.FixedTimeEquals(expected, offered);
    }

    private static bool TryDecodeHex(ReadOnlySpan<char> hex, Span<byte> destination)
    {
        if (hex.Length != destination.Length * 2)
        {
            return false;
        }

        for (var index = 0; index < destination.Length; index++)
        {
            if (!byte.TryParse(
                    hex.Slice(index * 2, 2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out var octet))
            {
                return false;
            }

            destination[index] = octet;
        }

        return true;
    }

    private static ReadOnlySpan<char> WithoutAlgorithmPrefix(string received)
    {
        var separator = received.IndexOf('=', StringComparison.Ordinal);

        return received.AsSpan(separator + 1).Trim();
    }

    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);
}
