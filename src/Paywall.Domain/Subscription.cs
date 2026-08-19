namespace Paywall.Domain;

/// <summary>
/// Recorrência ativa em um provedor. Guarda de quem é a referência: sem isso não dá para
/// saber a quem pedir o cancelamento quando existe mais de um meio de pagamento.
/// </summary>
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
