using System.Text;
using System.Text.Json;

namespace Paywall.Infrastructure.Providers.Crypto;

/// <summary>
/// Reproduz a forma canônica que a NOWPayments assina: as chaves em ordem alfabética, sem
/// espaços. O texto cru de cada valor é reaproveitado para o número voltar exatamente como
/// veio, já que reformatá-lo mudaria o resultado da assinatura.
/// </summary>
internal static class SortedJson
{
    public static string Canonicalize(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return json;
        }

        var builder = new StringBuilder("{");
        var first = true;

        foreach (var property in document.RootElement.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            if (!first)
            {
                builder.Append(',');
            }

            builder.Append('"').Append(property.Name).Append("\":").Append(property.Value.GetRawText());
            first = false;
        }

        return builder.Append('}').ToString();
    }
}
