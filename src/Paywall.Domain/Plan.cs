namespace Paywall.Domain;

/// <summary>
/// O que está à venda: preço, forma de cobrança e quanto acesso libera.
/// </summary>
public sealed class Plan
{
    public Plan(string id, string name, Money price, BillingMode billingMode, AccessDuration duration)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Plano precisa de um identificador.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Plano precisa de um nome.", nameof(name));
        }

        if (billingMode == BillingMode.Recurring && duration.IsLifetime)
        {
            throw new ArgumentException("Assinatura precisa de um período de renovação.", nameof(duration));
        }

        Id = id;
        Name = name;
        Price = price;
        BillingMode = billingMode;
        Duration = duration;
    }

    public string Id { get; }

    public string Name { get; }

    public Money Price { get; }

    public BillingMode BillingMode { get; }

    /// <summary>
    /// Para pagamento único, o prazo liberado. Para assinatura, o intervalo entre cobranças.
    /// </summary>
    public AccessDuration Duration { get; }
}
