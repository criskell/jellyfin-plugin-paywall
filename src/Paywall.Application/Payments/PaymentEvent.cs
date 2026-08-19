namespace Paywall.Application.Payments;

public enum PaymentEventKind
{
    Settled,
    Failed,
    Refunded,
    SubscriptionCanceled
}

public sealed record PaymentEvent(PaymentEventKind Kind, string ProviderReference, DateTimeOffset OccurredAt)
{
    public Guid? OrderId { get; init; }

    public string? SubscriptionReference { get; init; }
}
