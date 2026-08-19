using System.Security.Cryptography;
using System.Text;

namespace Paywall.Infrastructure.Providers.Crypto;

/// <summary>
/// Verificação de assinatura de webhook. Cada processador escolheu um algoritmo diferente,
/// mas todos assinam o corpo com um segredo compartilhado.
/// </summary>
internal static class WebhookSignature
{
    public static bool MatchesSha256(string body, string secret, string? received) =>
        Matches(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body)), received);

    public static bool MatchesSha512(string body, string secret, string? received) =>
        Matches(HMACSHA512.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body)), received);

    public static string Sha256Hex(string message, string secret) =>
        Convert.ToHexStringLower(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(message)));

    /// <summary>
    /// Comparação em tempo fixo: comparar por igualdade comum vazaria, pelo tempo de resposta,
    /// quantos caracteres iniciais um atacante acertou.
    /// </summary>
    public static bool Matches(byte[] expected, string? received)
    {
        if (string.IsNullOrWhiteSpace(received))
        {
            return false;
        }

        var offered = received.Contains('=', StringComparison.Ordinal)
            ? received[(received.IndexOf('=', StringComparison.Ordinal) + 1)..]
            : received;

        Span<byte> parsed = stackalloc byte[expected.Length];

        return Convert.FromHexString(offered.Trim()).AsSpan().TryCopyTo(parsed)
               && CryptographicOperations.FixedTimeEquals(expected, parsed);
    }
}
