namespace Paywall.Domain;

/// <summary>
/// Uma tentativa de pagamento. Guarda a referência externa que liga o webhook do provedor ao usuário.
/// </summary>
public sealed class Order
{
    private Order(
        Guid id,
        Guid userId,
        string planId,
        string providerKey,
        Money amount,
        OrderStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset? settledAt,
        string? providerReference)
    {
        Id = id;
        UserId = userId;
        PlanId = planId;
        ProviderKey = providerKey;
        Amount = amount;
        Status = status;
        CreatedAt = createdAt;
        SettledAt = settledAt;
        ProviderReference = providerReference;
    }

    public Guid Id { get; }

    public Guid UserId { get; }

    public string PlanId { get; }

    public string ProviderKey { get; }

    public Money Amount { get; }

    public OrderStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset? SettledAt { get; private set; }

    /// <summary>Identificador da cobrança no provedor. Preenchido depois que o checkout é aberto.</summary>
    public string? ProviderReference { get; private set; }

    public static Order Open(Guid id, Guid userId, Plan plan, string providerKey, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (string.IsNullOrWhiteSpace(providerKey))
        {
            throw new ArgumentException("Pedido precisa de um provedor.", nameof(providerKey));
        }

        return new Order(id, userId, plan.Id, providerKey, plan.Price, OrderStatus.Pending, createdAt, null, null);
    }

    public static Order Restore(
        Guid id,
        Guid userId,
        string planId,
        string providerKey,
        Money amount,
        OrderStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset? settledAt,
        string? providerReference) =>
        new(id, userId, planId, providerKey, amount, status, createdAt, settledAt, providerReference);

    public void TrackAs(string providerReference)
    {
        if (string.IsNullOrWhiteSpace(providerReference))
        {
            throw new ArgumentException("Referência do provedor é obrigatória.", nameof(providerReference));
        }

        ProviderReference = providerReference;
    }

    /// <summary>
    /// Confirma o pagamento. Retorna falso quando o pedido já saiu de pendente, o que torna
    /// reentrega de webhook inofensiva.
    /// </summary>
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

    public void Fail(DateTimeOffset at) => Close(OrderStatus.Failed, at);

    public void Cancel(DateTimeOffset at) => Close(OrderStatus.Canceled, at);

    public void Refund(DateTimeOffset at)
    {
        Status = OrderStatus.Refunded;
        SettledAt = at;
    }

    private void Close(OrderStatus status, DateTimeOffset at)
    {
        if (Status != OrderStatus.Pending)
        {
            return;
        }

        Status = status;
        SettledAt = at;
    }
}
