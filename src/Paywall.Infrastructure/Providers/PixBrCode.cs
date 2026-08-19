using System.Globalization;
using System.Text;

namespace Paywall.Infrastructure.Providers;

public static class PixBrCode
{
    private const string PixGui = "BR.GOV.BCB.PIX";

    public static string Build(PixBrCodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var merchantAccount = Field("00", PixGui) + Field("01", request.Key);

        var additionalData = Field("05", ToPrintableAscii(request.TransactionId, 25, fallback: "***"));

        var payload = new StringBuilder()
            .Append(Field("00", "01"))
            .Append(Field("26", merchantAccount))
            .Append(Field("52", "0000"))
            .Append(Field("53", "986"))
            .Append(Field("54", request.Amount.ToString("0.00", CultureInfo.InvariantCulture)))
            .Append(Field("58", "BR"))
            .Append(Field("59", ToPrintableAscii(request.PayeeName, 25, fallback: "RECEBEDOR")))
            .Append(Field("60", ToPrintableAscii(request.PayeeCity, 15, fallback: "SAO PAULO")))
            .Append(Field("62", additionalData))
            .Append("6304")
            .ToString();

        return payload + Crc16(payload);
    }

    private static string Field(string id, string value) =>
        id + value.Length.ToString("D2", CultureInfo.InvariantCulture) + value;

    private static string ToPrintableAscii(string? value, int maxLength, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character) || character is ' ' or '.' or '-')
            {
                builder.Append(char.ToUpperInvariant(character));
            }
        }

        var cleaned = builder.ToString().Trim();

        if (cleaned.Length == 0)
        {
            return fallback;
        }

        return cleaned.Length > maxLength ? cleaned[..maxLength].Trim() : cleaned;
    }

    private static string Crc16(string payload)
    {
        const ushort polynomial = 0x1021;
        var crc = (ushort)0xFFFF;

        foreach (var octet in Encoding.ASCII.GetBytes(payload))
        {
            crc ^= (ushort)(octet << 8);

            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ polynomial) : (ushort)(crc << 1);
            }
        }

        return crc.ToString("X4", CultureInfo.InvariantCulture);
    }
}

public sealed record PixBrCodeRequest(string Key, decimal Amount, string TransactionId)
{
    public string? PayeeName { get; init; }

    public string? PayeeCity { get; init; }
}
