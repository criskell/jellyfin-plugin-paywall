namespace Paywall.Domain;

public sealed class Order
{
    private Order(
        Guid id,
        Guid userId,
        PlanTerms terms,
        string providerKey,
        OrderStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset? settledAt,
        string? providerReference)
    {
        Id = id;
        UserId = userId;
        Terms = terms;
        ProviderKey = providerKey;
        Status = status;
        CreatedAt = createdAt;
        SettledAt = settledAt;
        ProviderReference = providerReference;
    }

    public Guid Id { get; }

    public Guid UserId { get; }

    public PlanTerms Terms { get; }

    public string ProviderKey { get; }

    public OrderStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset? SettledAt { get; private set; }

    public string? ProviderReference { get; private set; }

    public static Order Open(Guid id, Guid userId, Plan plan, string providerKey, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (string.IsNullOrWhiteSpace(providerKey))
        {
            throw new ArgumentException("Pedido precisa de um provedor.", nameof(providerKey));
        }

        return new Order(id, userId, PlanTerms.Of(plan), providerKey, OrderStatus.Pending, createdAt, null, null);
    }

    public static Order Renew(
        Guid id,
        Guid userId,
        PlanTerms terms,
        string providerKey,
        DateTimeOffset createdAt) =>
        new(id, userId, terms, providerKey, OrderStatus.Pending, createdAt, null, null);

    public static Order Restore(
        Guid id,
        Guid userId,
        PlanTerms terms,
        string providerKey,
        OrderStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset? settledAt,
        string? providerReference) =>
        new(id, userId, terms, providerKey, status, createdAt, settledAt, providerReference);

    public void TrackAs(string providerReference)
    {
        if (string.IsNullOrWhiteSpace(providerReference))
        {
            throw new ArgumentException("Referência do provedor é obrigatória.", nameof(providerReference));
        }

        ProviderReference = providerReference;
    }

    public bool TrySettle(DateTimeOffset paidAt)
    {
        if (Status != OrderStatus.Pending)
        {
            return false;
        }

        Status = OrderStatus.Paid;
        SettledAt = paidAt;
        return true;
    }

    public void Fail(DateTimeOffset at)
    {
        if (Status != OrderStatus.Pending)
        {
            return;
        }

        Status = OrderStatus.Failed;
        SettledAt = at;
    }

    public void Refund(DateTimeOffset at)
    {
        Status = OrderStatus.Refunded;
        SettledAt = at;
    }
}
