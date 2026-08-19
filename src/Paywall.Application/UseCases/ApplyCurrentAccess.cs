using Paywall.Application.Ports;
using Paywall.Domain;

namespace Paywall.Application.UseCases;

/// <summary>
/// Decide e aplica a situação de um usuário agora. Usado na varredura periódica, quando uma
/// conta é criada e a cada login, de forma que ninguém fique liberado no intervalo entre
/// duas varreduras.
/// </summary>
public sealed class ApplyCurrentAccess(
    ISubscriberDirectory subscribers,
    IAccessGrantRepository grants,
    IAccessEnforcer enforcer,
    IPaywallSettings settings,
    IClock clock)
{
    public async Task<AccessDecision?> ExecuteAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!await subscribers.IsSubjectAsync(userId, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var grant = await grants.FindAsync(userId, cancellationToken).ConfigureAwait(false)
                    ?? AccessGrant.NeverPaid(userId);

        var decision = grant.IsActiveAt(clock.UtcNow, settings.GracePeriod)
            ? AccessDecision.Allow
            : AccessDecision.Deny;

        await enforcer.ApplyAsync(userId, decision, cancellationToken).ConfigureAwait(false);

        return decision;
    }
}
