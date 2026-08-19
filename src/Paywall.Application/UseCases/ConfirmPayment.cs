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
    IAccessEnforcer enforcer,
    IIdentifierFactory identifiers)
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

        var order = await ResolveOrderAsync(notification.ProviderKey, payment, cancellationToken)
            .ConfigureAwait(false);

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

    /// <summary>
    /// Casa a notificação com um pedido. A cobrança que a recorrência gera sozinha todo mês
    /// nunca teve pedido aberto por aqui, então nesse caso um pedido de renovação é criado.
    /// </summary>
    private async Task<Order?> ResolveOrderAsync(
        string providerKey,
        PaymentEvent payment,
        CancellationToken cancellationToken)
    {
        if (payment.OrderId is { } orderId)
        {
            var byId = await orders.FindAsync(orderId, cancellationToken).ConfigureAwait(false);

            if (byId is { Status: OrderStatus.Pending })
            {
                return byId;
            }
        }

        var byReference = await orders
            .FindByReferenceAsync(providerKey, payment.ProviderReference, cancellationToken)
            .ConfigureAwait(false);

        if (byReference is not null)
        {
            return byReference;
        }

        if (payment.SubscriptionReference is not { } subscription)
        {
            return null;
        }

        // A primeira cobrança de uma assinatura nova: o pedido foi aberto com o id da recorrência.
        var bySubscription = await orders
            .FindByReferenceAsync(providerKey, subscription, cancellationToken)
            .ConfigureAwait(false);

        if (bySubscription is { Status: OrderStatus.Pending })
        {
            return bySubscription;
        }

        return await OpenRenewalAsync(providerKey, payment, subscription, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Order?> OpenRenewalAsync(
        string providerKey,
        PaymentEvent payment,
        string subscription,
        CancellationToken cancellationToken)
    {
        var grant = await grants.FindBySubscriptionAsync(subscription, cancellationToken).ConfigureAwait(false);

        if (grant?.PlanId is null || plans.Find(grant.PlanId) is not { } plan)
        {
            return null;
        }

        var renewal = Order.Open(identifiers.NewId(), grant.UserId, plan, providerKey, payment.OccurredAt);
        renewal.TrackAs(payment.ProviderReference);

        return renewal;
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
