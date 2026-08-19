using System.Text;
using System.Text.Json;

namespace Paywall.Infrastructure.Providers.Crypto;

internal static class NowPaymentsSignedForm
{
    public static string Of(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return json;
        }

        var form = new StringBuilder("{");
        var first = true;

        foreach (var field in document.RootElement.EnumerateObject().OrderBy(f => f.Name, StringComparer.Ordinal))
        {
            if (!first)
            {
                form.Append(',');
            }

            AppendUnalteredValue(form, field);
            first = false;
        }

        return form.Append('}').ToString();
    }

    private static void AppendUnalteredValue(StringBuilder form, JsonProperty field) =>
        form.Append('"').Append(field.Name).Append("\":").Append(field.Value.GetRawText());
}
