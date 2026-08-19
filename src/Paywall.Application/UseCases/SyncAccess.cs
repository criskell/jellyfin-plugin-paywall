using Paywall.Application.Ports;

namespace Paywall.Application.UseCases;

public sealed record AccessSyncReport(int Allowed, int Denied);

public sealed class SyncAccess(ISubscriberDirectory subscribers, ApplyCurrentAccess applyAccess)
{
    public async Task<AccessSyncReport> ExecuteAsync(CancellationToken cancellationToken)
    {
        var everyone = await subscribers.ListAsync(cancellationToken).ConfigureAwait(false);

        var allowed = 0;
        var denied = 0;

        foreach (var subscriber in everyone)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var decision = await applyAccess.ExecuteAsync(subscriber.UserId, cancellationToken)
                .ConfigureAwait(false);

            if (decision == AccessDecision.Allow)
            {
                allowed++;
            }
            else if (decision == AccessDecision.Deny)
            {
                denied++;
            }
        }

        return new AccessSyncReport(allowed, denied);
    }
}
