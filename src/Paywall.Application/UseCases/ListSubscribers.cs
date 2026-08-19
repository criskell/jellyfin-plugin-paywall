using Paywall.Application.Ports;
using Paywall.Domain;

namespace Paywall.Application.UseCases;

public sealed record SubscriberStatusView(
    Guid UserId,
    string Name,
    bool HasAccess,
    string? PlanId,
    DateTimeOffset? ExpiresAt,
    bool IsLifetime,
    bool HasActiveSubscription);

public sealed class ListSubscribers(
    ISubscriberDirectory subscribers,
    IAccessGrantRepository grants,
    IPaywallSettings settings,
    IClock clock)
{
    public async Task<IReadOnlyCollection<SubscriberStatusView>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var grace = settings.GracePeriod;
        var everyone = await subscribers.ListAsync(cancellationToken).ConfigureAwait(false);

        var statuses = new List<SubscriberStatusView>(everyone.Count);

        foreach (var subscriber in everyone)
        {
            var grant = await grants.FindAsync(subscriber.UserId, cancellationToken).ConfigureAwait(false)
                        ?? AccessGrant.NeverPaid(subscriber.UserId);

            statuses.Add(new SubscriberStatusView(
                subscriber.UserId,
                subscriber.Name,
                grant.IsActiveAt(now, grace),
                grant.PlanId,
                grant.ExpiresAt,
                grant.PlanId is not null && grant.ExpiresAt is null,
                grant.Subscription is not null));
        }

        return statuses;
    }
}
