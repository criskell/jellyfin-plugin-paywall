using Paywall.Application.Payments;
using Paywall.Application.Ports;
using Paywall.Domain;

namespace Paywall.Application.UseCases;

/// <summary>
/// Corta o acesso na hora e encerra a recorrência no provedor que a emitiu, para o usuário
/// não continuar sendo debitado por algo que não pode mais assistir.
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

        if (grant.Subscription is { } subscription)
        {
            await CancelAsync(subscription, cancellationToken).ConfigureAwait(false);
        }

        grant.Revoke(clock.UtcNow);

        await grants.SaveAsync(grant, cancellationToken).ConfigureAwait(false);
        await enforcer.ApplyAsync(userId, AccessDecision.Deny, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Só o provedor que emitiu a recorrência sabe cancelá-la. Se ele não está mais
    /// configurado, o acesso é cortado assim mesmo em vez de a revogação inteira falhar.
    /// </summary>
    private async Task CancelAsync(Subscription subscription, CancellationToken cancellationToken)
    {
        IPaymentProvider provider;

        try
        {
            provider = providers.Resolve(subscription.ProviderKey);
        }
        catch (PaymentProviderNotFoundException)
        {
            return;
        }

        if (provider is ISupportsSubscriptionCancellation cancellable)
        {
            await cancellable.CancelSubscriptionAsync(subscription.Reference, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
