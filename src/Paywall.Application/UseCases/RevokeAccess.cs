using Paywall.Application.Payments;
using Paywall.Application.Ports;

namespace Paywall.Application.UseCases;

/// <summary>
/// Corta o acesso na hora e, quando o provedor permite, encerra também a recorrência
/// para o usuário não continuar sendo debitado.
/// </summary>
public sealed class RevokeAccess(
    IAccessGrantRepository grants,
    IAccessEnforcer enforcer,
    IPaymentProviderRegistry providers,
    IClock clock)
{
    public async Task ExecuteAsync(Guid userId, CancellationToken cancellationToken)
    {
        var grant = await grants.FindAsync(userId, cancellationToken).ConfigureAwait(false);

        if (grant is null)
        {
            await enforcer.ApplyAsync(userId, AccessDecision.Deny, cancellationToken).ConfigureAwait(false);
            return;
        }

        await TryCancelSubscriptionAsync(grant.SubscriptionReference, cancellationToken).ConfigureAwait(false);

        grant.Revoke(clock.UtcNow);

        await grants.SaveAsync(grant, cancellationToken).ConfigureAwait(false);
        await enforcer.ApplyAsync(userId, AccessDecision.Deny, cancellationToken).ConfigureAwait(false);
    }

    private async Task TryCancelSubscriptionAsync(string? subscriptionReference, CancellationToken cancellationToken)
    {
        if (subscriptionReference is null)
        {
            return;
        }

        foreach (var provider in providers.Available.OfType<ISupportsSubscriptionCancellation>())
        {
            await provider.CancelSubscriptionAsync(subscriptionReference, cancellationToken).ConfigureAwait(false);
        }
    }
}
