namespace Paywall.Domain;

/// <summary>
/// Valor monetário em unidades mínimas (centavos), evitando ponto flutuante em dinheiro.
/// </summary>
public readonly record struct Money
{
    private Money(long cents, string currency)
    {
        Cents = cents;
        Currency = currency;
    }

    public long Cents { get; }

    public string Currency { get; }

    public decimal Amount => Cents / 100m;

    public static Money Of(long cents, string currency = "BRL")
    {
        if (cents < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cents), "Valor não pode ser negativo.");
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Moeda é obrigatória.", nameof(currency));
        }

        return new Money(cents, currency.ToUpperInvariant());
    }

    public override string ToString() => $"{Currency} {Amount:0.00}";
}
