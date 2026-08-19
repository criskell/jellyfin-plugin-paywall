namespace Paywall.Domain;

/// <summary>
/// Direito de acesso de um usuário. Agregado consultado para liberar ou bloquear a biblioteca.
/// </summary>
public sealed class AccessGrant
{
    private AccessGrant(Guid userId, string? planId, DateTimeOffset? expiresAt, string? subscriptionReference)
    {
        UserId = userId;
        PlanId = planId;
        ExpiresAt = expiresAt;
        SubscriptionReference = subscriptionReference;
    }

    public Guid UserId { get; }

    public string? PlanId { get; private set; }

    /// <summary>Nulo com <see cref="PlanId"/> preenchido significa acesso vitalício.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>Identificador da recorrência no provedor, quando o plano é assinatura.</summary>
    public string? SubscriptionReference { get; private set; }

    public static AccessGrant NeverPaid(Guid userId) => new(userId, null, null, null);

    public static AccessGrant Restore(
        Guid userId,
        string? planId,
        DateTimeOffset? expiresAt,
        string? subscriptionReference) => new(userId, planId, expiresAt, subscriptionReference);

    public bool IsActiveAt(DateTimeOffset instant, TimeSpan grace)
    {
        if (PlanId is null)
        {
            return false;
        }

        return ExpiresAt is null || ExpiresAt.Value.Add(grace) > instant;
    }

    /// <summary>
    /// Aplica um pagamento aprovado. Renovação antecipada soma ao prazo restante em vez de descartá-lo.
    /// </summary>
    public void Extend(Plan plan, DateTimeOffset paidAt)
    {
        ArgumentNullException.ThrowIfNull(plan);

        PlanId = plan.Id;

        if (plan.Duration.IsLifetime)
        {
            ExpiresAt = null;
            return;
        }

        var startsFrom = ExpiresAt is { } current && current > paidAt ? current : paidAt;
        ExpiresAt = startsFrom.AddDays(plan.Duration.Days!.Value);
    }

    public void AttachSubscription(string? subscriptionReference) => SubscriptionReference = subscriptionReference;

    /// <summary>Corta o acesso imediatamente, sem apagar o histórico de plano.</summary>
    public void Revoke(DateTimeOffset at)
    {
        ExpiresAt = at;
        SubscriptionReference = null;
    }
}
