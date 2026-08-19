namespace Paywall.Domain;

public sealed class AccessGrant
{
    private AccessGrant(
        Guid userId,
        PlanTerms? terms,
        DateTimeOffset? expiresAt,
        Subscription? subscription,
        bool revoked)
    {
        UserId = userId;
        Terms = terms;
        ExpiresAt = expiresAt;
        Subscription = subscription;
        Revoked = revoked;
    }

    public Guid UserId { get; }

    public PlanTerms? Terms { get; private set; }

    public string? PlanId => Terms?.PlanId;

    public DateTimeOffset? ExpiresAt { get; private set; }

    public Subscription? Subscription { get; private set; }

    public bool Revoked { get; private set; }

    public static AccessGrant NeverPaid(Guid userId) => new(userId, null, null, null, false);

    public static AccessGrant Restore(
        Guid userId,
        PlanTerms? terms,
        DateTimeOffset? expiresAt,
        Subscription? subscription,
        bool revoked) => new(userId, terms, expiresAt, subscription, revoked);

    public bool IsActiveAt(DateTimeOffset instant, TimeSpan grace)
    {
        if (Terms is null || Revoked)
        {
            return false;
        }

        return ExpiresAt is null || ExpiresAt.Value.Add(grace) > instant;
    }

    public void Extend(PlanTerms terms, DateTimeOffset paidAt)
    {
        ArgumentNullException.ThrowIfNull(terms);

        Terms = terms;
        Revoked = false;

        if (terms.Duration.IsLifetime)
        {
            ExpiresAt = null;
            return;
        }

        ExpiresAt = UnusedTimeEndsAt(paidAt).AddDays(terms.Duration.Days!.Value);
    }

    public void AttachSubscription(Subscription? subscription) => Subscription = subscription;

    public void Revoke(DateTimeOffset at)
    {
        Revoked = true;
        ExpiresAt = at;
        Subscription = null;
    }

    private DateTimeOffset UnusedTimeEndsAt(DateTimeOffset paidAt) =>
        ExpiresAt is { } current && current > paidAt ? current : paidAt;
}
