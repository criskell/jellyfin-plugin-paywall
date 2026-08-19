namespace Paywall.Domain;

public sealed class AccessGrant
{
    private AccessGrant(Guid userId, string? planId, DateTimeOffset? expiresAt, Subscription? subscription)
    {
        UserId = userId;
        PlanId = planId;
        ExpiresAt = expiresAt;
        Subscription = subscription;
    }

    public Guid UserId { get; }

    public string? PlanId { get; private set; }

    public DateTimeOffset? ExpiresAt { get; private set; }

    public Subscription? Subscription { get; private set; }

    public static AccessGrant NeverPaid(Guid userId) => new(userId, null, null, null);

    public static AccessGrant Restore(
        Guid userId,
        string? planId,
        DateTimeOffset? expiresAt,
        Subscription? subscription) => new(userId, planId, expiresAt, subscription);

    public bool IsActiveAt(DateTimeOffset instant, TimeSpan grace)
    {
        if (PlanId is null)
        {
            return false;
        }

        return ExpiresAt is null || ExpiresAt.Value.Add(grace) > instant;
    }

    public void Extend(Plan plan, DateTimeOffset paidAt)
    {
        ArgumentNullException.ThrowIfNull(plan);

        PlanId = plan.Id;

        if (plan.Duration.IsLifetime)
        {
            ExpiresAt = null;
            return;
        }

        ExpiresAt = UnusedTimeEndsAt(paidAt).AddDays(plan.Duration.Days!.Value);
    }

    public void AttachSubscription(Subscription? subscription) => Subscription = subscription;

    public void Revoke(DateTimeOffset at)
    {
        ExpiresAt = at;
        Subscription = null;
    }

    private DateTimeOffset UnusedTimeEndsAt(DateTimeOffset paidAt) =>
        ExpiresAt is { } current && current > paidAt ? current : paidAt;
}
