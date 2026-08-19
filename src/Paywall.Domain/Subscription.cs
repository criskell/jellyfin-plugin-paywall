namespace Paywall.Domain;

public sealed record Subscription
{
    public Subscription(string providerKey, string reference)
    {
        if (string.IsNullOrWhiteSpace(providerKey))
        {
            throw new ArgumentException("Assinatura precisa do provedor.", nameof(providerKey));
        }

        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new ArgumentException("Assinatura precisa da referência.", nameof(reference));
        }

        ProviderKey = providerKey;
        Reference = reference;
    }

    public string ProviderKey { get; }

    public string Reference { get; }
}
