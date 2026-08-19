namespace Paywall.Application.Payments;

public enum PaymentEventKind
{
    Settled,
    Failed,
    Refunded,
    SubscriptionCanceled
}

/// <summary>
/// Leitura do provedor sobre o que aconteceu com uma cobrança, normalizada entre integrações.
/// </summary>
public sealed record PaymentEvent(PaymentEventKind Kind, string ProviderReference, DateTimeOffset OccurredAt)
{
    /// <summary>Preenchido quando o provedor devolve o identificador que enviamos no checkout.</summary>
    public Guid? OrderId { get; init; }

    /// <summary>Identificador da recorrência, para planos de assinatura.</summary>
    public string? SubscriptionReference { get; init; }
}
