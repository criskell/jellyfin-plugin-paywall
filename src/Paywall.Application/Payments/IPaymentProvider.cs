using Paywall.Domain;

namespace Paywall.Application.Payments;

public interface IPaymentProvider
{
    string Key { get; }

    string DisplayName { get; }

    IReadOnlyCollection<BillingMode> SupportedModes { get; }

    bool IsConfigured { get; }

    Task<CheckoutTicket> StartCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken);

    Task<PaymentEvent?> ReadPaymentEventAsync(InboundNotification notification, CancellationToken cancellationToken);
}

public interface ISupportsSubscriptionCancellation
{
    Task CancelSubscriptionAsync(string subscriptionReference, CancellationToken cancellationToken);
}
