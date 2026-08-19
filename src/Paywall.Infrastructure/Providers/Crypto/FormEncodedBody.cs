namespace Paywall.Infrastructure.Providers.Crypto;

internal static class FormEncodedBody
{
    public static IReadOnlyDictionary<string, string> Parse(string body)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=', StringComparison.Ordinal);

            if (separator <= 0)
            {
                continue;
            }

            fields[Decode(pair[..separator])] = Decode(pair[(separator + 1)..]);
        }

        return fields;
    }

    private static string Decode(string value) => Uri.UnescapeDataString(value.Replace('+', ' '));
}
