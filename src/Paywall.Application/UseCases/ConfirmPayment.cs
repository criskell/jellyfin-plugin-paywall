using Paywall.Application.Payments;
using Paywall.Application.Ports;
using Paywall.Domain;

namespace Paywall.Application.UseCases;

public enum ConfirmPaymentOutcome
{
    Ignored,

    Unmatched,

    AlreadyProcessed,

    AccessGranted,
    AccessRevoked,
    PaymentFailed,
    SubscriptionEnded
}

public sealed class ConfirmPayment(
    IPaymentProviderRegistry providers,
    IOrderRepository orders,
    IAccessGrantRepository grants,
    IAccessEnforcer enforcer,
    IIdentifierFactory identifiers)
{
    public async Task<ConfirmPaymentOutcome> ExecuteAsync(
        InboundNotification notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var provider = providers.Resolve(notification.ProviderKey);
        var payment = await provider.ReadPaymentEventAsync(notification, cancellationToken).ConfigureAwait(false);

        if (payment is null)
        {
            return ConfirmPaymentOutcome.Ignored;
        }

        var providerKey = notification.ProviderKey;

        return payment.Kind == PaymentEventKind.SubscriptionCanceled
            ? await ReleaseSubscriptionAsync(providerKey, payment, cancellationToken).ConfigureAwait(false)
            : await ApplyToOrderAsync(providerKey, payment, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ConfirmPaymentOutcome> ApplyToOrderAsync(
        string providerKey,
        PaymentEvent payment,
        CancellationToken cancellationToken)
    {
        var order = await ResolveOrderAsync(providerKey, payment, cancellationToken).ConfigureAwait(false);

        if (order is null)
        {
            return ConfirmPaymentOutcome.Unmatched;
        }

        return payment.Kind switch
        {
            PaymentEventKind.Settled =>
                await SettleAsync(order, payment, providerKey, cancellationToken).ConfigureAwait(false),
            PaymentEventKind.Refunded => await RevokeAsync(order, payment, cancellationToken).ConfigureAwait(false),
            PaymentEventKind.Failed => await FailAsync(order, payment, cancellationToken).ConfigureAwait(false),
            _ => ConfirmPaymentOutcome.Ignored
        };
    }

    private async Task<ConfirmPaymentOutcome> ReleaseSubscriptionAsync(
        string providerKey,
        PaymentEvent payment,
        CancellationToken cancellationToken)
    {
        if (payment.SubscriptionReference is not { } reference)
        {
            return ConfirmPaymentOutcome.Ignored;
        }

        var subscription = new Subscription(providerKey, reference);
        var grant = await grants.FindBySubscriptionAsync(subscription, cancellationToken).ConfigureAwait(false);

        if (grant is null)
        {
            return ConfirmPaymentOutcome.Unmatched;
        }

        grant.AttachSubscription(null);
        await grants.SaveAsync(grant, cancellationToken).ConfigureAwait(false);

        return ConfirmPaymentOutcome.SubscriptionEnded;
    }

    private async Task<Order?> ResolveOrderAsync(
        string providerKey,
        PaymentEvent payment,
        CancellationToken cancellationToken)
    {
        return await FindAwaitingPaymentAsync(payment, cancellationToken).ConfigureAwait(false)
               ?? await FindByChargeAsync(providerKey, payment, cancellationToken).ConfigureAwait(false)
               ?? await FindFirstChargeOfNewSubscriptionAsync(providerKey, payment, cancellationToken)
                   .ConfigureAwait(false)
               ?? await OpenRenewalOfExistingSubscriptionAsync(providerKey, payment, cancellationToken)
                   .ConfigureAwait(false);
    }

    private async Task<Order?> FindAwaitingPaymentAsync(PaymentEvent payment, CancellationToken cancellationToken)
    {
        if (payment.OrderId is not { } orderId)
        {
            return null;
        }

        var order = await orders.FindAsync(orderId, cancellationToken).ConfigureAwait(false);

        return order is { Status: OrderStatus.Pending } ? order : null;
    }

    private Task<Order?> FindByChargeAsync(
        string providerKey,
        PaymentEvent payment,
        CancellationToken cancellationToken) =>
        orders.FindByReferenceAsync(providerKey, payment.ProviderReference, cancellationToken);

    private async Task<Order?> FindFirstChargeOfNewSubscriptionAsync(
        string providerKey,
        PaymentEvent payment,
        CancellationToken cancellationToken)
    {
        if (payment.SubscriptionReference is not { } reference)
        {
            return null;
        }

        var order = await orders.FindByReferenceAsync(providerKey, reference, cancellationToken)
            .ConfigureAwait(false);

        return order is { Status: OrderStatus.Pending } ? order : null;
    }

    private async Task<Order?> OpenRenewalOfExistingSubscriptionAsync(
        string providerKey,
        PaymentEvent payment,
        CancellationToken cancellationToken)
    {
        if (payment.SubscriptionReference is not { } reference)
        {
            return null;
        }

        var subscription = new Subscription(providerKey, reference);
        var grant = await grants.FindBySubscriptionAsync(subscription, cancellationToken).ConfigureAwait(false);

        if (grant?.Terms is not { } terms)
        {
            return null;
        }

        var renewal = Order.Renew(
            identifiers.NewId(),
            grant.UserId,
            terms,
            subscription.ProviderKey,
            payment.OccurredAt);
        renewal.TrackAs(payment.ProviderReference);

        return renewal;
    }

    private async Task<ConfirmPaymentOutcome> SettleAsync(
        Order order,
        PaymentEvent payment,
        string providerKey,
        CancellationToken cancellationToken)
    {
        if (!order.TrySettle(payment.OccurredAt))
        {
            return ConfirmPaymentOutcome.AlreadyProcessed;
        }

        var grant = await grants.FindAsync(order.UserId, cancellationToken).ConfigureAwait(false)
                    ?? AccessGrant.NeverPaid(order.UserId);

        grant.Extend(order.Terms, payment.OccurredAt);
        grant.AttachSubscription(payment.SubscriptionReference is { } reference
            ? new Subscription(providerKey, reference)
            : null);

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

}
