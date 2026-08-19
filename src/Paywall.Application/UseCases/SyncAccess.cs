using Paywall.Application.Ports;
using Paywall.Domain;

namespace Paywall.Application.UseCases;

public sealed record AccessSyncReport(int Allowed, int Denied);

/// <summary>
/// Reconcilia o estado real do servidor com os vencimentos. É o que corta quem parou de pagar,
/// e o que conserta divergências se um webhook se perdeu.
/// </summary>
public sealed class SyncAccess(
    ISubscriberDirectory subscribers,
    IAccessGrantRepository grants,
    IAccessEnforcer enforcer,
    IPaywallSettings settings,
    IClock clock)
{
    public async Task<AccessSyncReport> ExecuteAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var grace = settings.GracePeriod;
        var everyone = await subscribers.ListAsync(cancellationToken).ConfigureAwait(false);

        var allowed = 0;
        var denied = 0;

        foreach (var subscriber in everyone)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var grant = await grants.FindAsync(subscriber.UserId, cancellationToken).ConfigureAwait(false)
                        ?? AccessGrant.NeverPaid(subscriber.UserId);

            var decision = grant.IsActiveAt(now, grace) ? AccessDecision.Allow : AccessDecision.Deny;
            await enforcer.ApplyAsync(subscriber.UserId, decision, cancellationToken).ConfigureAwait(false);

            if (decision == AccessDecision.Allow)
            {
                allowed++;
            }
            else
            {
                denied++;
            }
        }

        return new AccessSyncReport(allowed, denied);
    }
}
