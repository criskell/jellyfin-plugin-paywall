using Paywall.Application;
using Paywall.Application.Payments;
using Paywall.Application.Ports;
using Paywall.Domain;

namespace Paywall.Tests;

internal sealed class FixedClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; } = now;
}

internal sealed class SequentialIdentifiers : IIdentifierFactory
{
    private int _next;

    public Guid NewId() => new($"00000000-0000-0000-0000-{++_next:D12}");
}

internal sealed class InMemoryOrders : IOrderRepository
{
    private readonly Dictionary<Guid, Order> _orders = [];

    public Task<Order?> FindAsync(Guid orderId, CancellationToken cancellationToken) =>
        Task.FromResult(_orders.GetValueOrDefault(orderId));

    public Task<Order?> FindByReferenceAsync(string providerKey, string reference, CancellationToken cancellationToken) =>
        Task.FromResult(_orders.Values.FirstOrDefault(order =>
            order.ProviderKey == providerKey && order.ProviderReference == reference));

    public Task SaveAsync(Order order, CancellationToken cancellationToken)
    {
        _orders[order.Id] = order;
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryGrants : IAccessGrantRepository
{
    private readonly Dictionary<Guid, AccessGrant> _grants = [];

    public Task<AccessGrant?> FindAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(_grants.GetValueOrDefault(userId));

    public Task<AccessGrant?> FindBySubscriptionAsync(Subscription subscription, CancellationToken cancellationToken) =>
        Task.FromResult(_grants.Values.FirstOrDefault(grant => grant.Subscription == subscription));

    public Task SaveAsync(AccessGrant grant, CancellationToken cancellationToken)
    {
        _grants[grant.UserId] = grant;
        return Task.CompletedTask;
    }
}

internal sealed class RecordingEnforcer : IAccessEnforcer
{
    public List<(Guid UserId, AccessDecision Decision)> Applied { get; } = [];

    public Task ApplyAsync(Guid userId, AccessDecision decision, CancellationToken cancellationToken)
    {
        Applied.Add((userId, decision));
        return Task.CompletedTask;
    }
}

internal sealed class FixedCatalog(params Plan[] plans) : IPlanCatalog
{
    public IReadOnlyCollection<Plan> All { get; } = plans;

    public Plan? Find(string planId) => All.FirstOrDefault(plan => plan.Id == planId);
}

internal sealed class FakeDirectory(params Guid[] subjects) : ISubscriberDirectory
{
    public Task<IReadOnlyCollection<Subscriber>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<Subscriber>>(
            subjects.Select(id => new Subscriber(id, id.ToString("N"))).ToArray());

    public Task<bool> IsSubjectAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(subjects.Contains(userId));
}

internal sealed class FixedSettings(TimeSpan grace) : IPaywallSettings
{
    public TimeSpan GracePeriod { get; } = grace;
}

internal sealed class FakeRegistry(params IPaymentProvider[] providers) : IPaymentProviderRegistry
{
    public IReadOnlyCollection<IPaymentProvider> Available { get; } = providers;

    public IPaymentProvider Resolve(string key) =>
        providers.FirstOrDefault(provider => provider.Key == key)
        ?? throw new PaymentProviderNotFoundException(key);
}

internal sealed class CancellableProvider(string key) : IPaymentProvider, ISupportsSubscriptionCancellation
{
    public List<string> Canceled { get; } = [];

    public string Key { get; } = key;

    public string DisplayName => Key;

    public IReadOnlyCollection<BillingMode> SupportedModes { get; } = [BillingMode.Recurring];

    public bool IsConfigured => true;

    public Task<CheckoutTicket> StartCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new CheckoutTicket("ref", new PaymentInstructions()));

    public Task<PaymentEvent?> ReadPaymentEventAsync(InboundNotification n, CancellationToken cancellationToken) =>
        Task.FromResult<PaymentEvent?>(null);

    public Task CancelSubscriptionAsync(string subscriptionReference, CancellationToken cancellationToken)
    {
        Canceled.Add(subscriptionReference);
        return Task.CompletedTask;
    }
}

internal sealed class StubProvider(PaymentEvent? result) : IPaymentProvider, IPaymentProviderRegistry
{
    public string Key => "stub";

    public string DisplayName => "Stub";

    public IReadOnlyCollection<BillingMode> SupportedModes { get; } = [BillingMode.OneTime, BillingMode.Recurring];

    public bool IsConfigured => true;

    public IReadOnlyCollection<IPaymentProvider> Available => [this];

    public IPaymentProvider Resolve(string key) => this;

    public Task<CheckoutTicket> StartCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new CheckoutTicket("ref", new PaymentInstructions()));

    public Task<PaymentEvent?> ReadPaymentEventAsync(InboundNotification n, CancellationToken cancellationToken) =>
        Task.FromResult(result);
}
