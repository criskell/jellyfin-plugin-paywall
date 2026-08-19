using Paywall.Application.Payments;
using Paywall.Application.Ports;
using Paywall.Domain;

namespace Paywall.Application.UseCases;

public enum ConfirmPaymentOutcome
{
    /// <summary>Notificação legítima, mas sem efeito sobre cobranças (ping, evento irrelevante).</summary>
    Ignored,

    OrderNotFound,

    /// <summary>Reentrega de um webhook já processado.</summary>
    AlreadyProcessed,

    AccessGranted,
    AccessRevoked,
    PaymentFailed,
    SubscriptionEnded
}

/// <summary>
/// Único caminho pelo qual um pagamento vira acesso. Chamado pelo webhook de qualquer provedor.
/// </summary>
public sealed class ConfirmPayment(
    IPaymentProviderRegistry providers,
    IOrderRepository orders,
    IAccessGrantRepository grants,
    IPlanCatalog plans,
    IAccessEnforcer enforcer)
{
    public async Task<ConfirmPaymentOutcome> ExecuteAsync(
        InboundNotification notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var provider = providers.Resolve(notification.ProviderKey);
        var payment = await provider.InterpretAsync(notification, cancellationToken).ConfigureAwait(false);

        if (payment is null)
        {
            return ConfirmPaymentOutcome.Ignored;
        }

        var order = await LocateAsync(notification.ProviderKey, payment, cancellationToken).ConfigureAwait(false);

        if (order is null)
        {
            return ConfirmPaymentOutcome.OrderNotFound;
        }

        return payment.Kind switch
        {
            PaymentEventKind.Settled => await SettleAsync(order, payment, cancellationToken).ConfigureAwait(false),
            PaymentEventKind.Refunded => await RevokeAsync(order, payment, cancellationToken).ConfigureAwait(false),
            PaymentEventKind.Failed => await FailAsync(order, payment, cancellationToken).ConfigureAwait(false),
            PaymentEventKind.SubscriptionCanceled =>
                await DetachSubscriptionAsync(order, cancellationToken).ConfigureAwait(false),
            _ => ConfirmPaymentOutcome.Ignored
        };
    }

    private async Task<Order?> LocateAsync(
        string providerKey,
        PaymentEvent payment,
        CancellationToken cancellationToken)
    {
        if (payment.OrderId is { } orderId)
        {
            var byId = await orders.FindAsync(orderId, cancellationToken).ConfigureAwait(false);
            if (byId is not null)
            {
                return byId;
            }
        }

        return await orders
            .FindByReferenceAsync(providerKey, payment.ProviderReference, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<ConfirmPaymentOutcome> SettleAsync(
        Order order,
        PaymentEvent payment,
        CancellationToken cancellationToken)
    {
        if (!order.TrySettle(payment.OccurredAt))
        {
            return ConfirmPaymentOutcome.AlreadyProcessed;
        }

        var plan = plans.Find(order.PlanId) ?? throw new PlanNotFoundException(order.PlanId);
        var grant = await grants.FindAsync(order.UserId, cancellationToken).ConfigureAwait(false)
                    ?? AccessGrant.NeverPaid(order.UserId);

        grant.Extend(plan, payment.OccurredAt);
        grant.AttachSubscription(payment.SubscriptionReference);

        await orders.SaveAsync(order, cancellationToken).ConfigureAwait(false);
        await grants.SaveAsync(grant, cancellationToken).ConfigureAwait(false);
        await enforcer.ApplyAsync(order.UserId, AccessDecision.Allow, cancellationToken).ConfigureAwait(false);

        return ConfirmPaymentOutcome.AccessGranted;
    }

    private async Task<ConfirmPaymentOutcome> RevokeAsync(
        Order order,
        PaymentEvent payment,
        CancellationToken cancellationToken)
    {
        order.Refund(payment.OccurredAt);

        var grant = await grants.FindAsync(order.UserId, cancellationToken).ConfigureAwait(false);
        grant?.Revoke(payment.OccurredAt);

        await orders.SaveAsync(order, cancellationToken).ConfigureAwait(false);

        if (grant is not null)
        {
            await grants.SaveAsync(grant, cancellationToken).ConfigureAwait(false);
        }

        await enforcer.ApplyAsync(order.UserId, AccessDecision.Deny, cancellationToken).ConfigureAwait(false);

        return ConfirmPaymentOutcome.AccessRevoked;
    }

    private async Task<ConfirmPaymentOutcome> FailAsync(
        Order order,
        PaymentEvent payment,
        CancellationToken cancellationToken)
    {
        order.Fail(payment.OccurredAt);
        await orders.SaveAsync(order, cancellationToken).ConfigureAwait(false);

        return ConfirmPaymentOutcome.PaymentFailed;
    }

    /// <summary>
    /// Assinatura cancelada não corta o acesso na hora: o usuário fica até o fim do período já pago.
    /// </summary>
    private async Task<ConfirmPaymentOutcome> DetachSubscriptionAsync(Order order, CancellationToken cancellationToken)
    {
        var grant = await grants.FindAsync(order.UserId, cancellationToken).ConfigureAwait(false);

        if (grant is not null)
        {
            grant.AttachSubscription(null);
            await grants.SaveAsync(grant, cancellationToken).ConfigureAwait(false);
        }

        return ConfirmPaymentOutcome.SubscriptionEnded;
    }
}
