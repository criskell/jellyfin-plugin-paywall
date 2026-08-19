using Paywall.Application.Payments;
using Paywall.Application.Ports;
using Paywall.Application.UseCases;
using Paywall.Domain;
using Xunit;

namespace Paywall.Tests;

public class ConfirmPaymentTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Subscriber = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static Plan Monthly =>
        new("mensal", "Mensal", Money.Of(1990), BillingMode.Recurring, AccessDuration.OfDays(30));

    private static InboundNotification AnyNotification =>
        new("stub", new Dictionary<string, string>(), new Dictionary<string, string>(), "{}");

    [Fact]
    public async Task PagamentoAprovadoLiberaOAcesso()
    {
        var orders = new InMemoryOrders();
        var grants = new InMemoryGrants();
        var enforcer = new RecordingEnforcer();
        var order = Order.Open(Guid.NewGuid(), Subscriber, Monthly, "stub", Now);
        order.TrackAs("pay_1");
        await orders.SaveAsync(order, CancellationToken.None);

        var settled = new PaymentEvent(PaymentEventKind.Settled, "pay_1", Now);
        await Confirm(settled, orders, grants, enforcer);

        var grant = await grants.FindAsync(Subscriber, CancellationToken.None);
        Assert.Equal(Now.AddDays(30), grant?.ExpiresAt);
        Assert.Contains((Subscriber, AccessDecision.Allow), enforcer.Applied);
    }

    [Fact]
    public async Task WebhookReentregueNaoEstendeDeNovo()
    {
        var orders = new InMemoryOrders();
        var grants = new InMemoryGrants();
        var order = Order.Open(Guid.NewGuid(), Subscriber, Monthly, "stub", Now);
        order.TrackAs("pay_1");
        await orders.SaveAsync(order, CancellationToken.None);

        var settled = new PaymentEvent(PaymentEventKind.Settled, "pay_1", Now);
        await Confirm(settled, orders, grants, new RecordingEnforcer());
        var outcome = await Confirm(settled, orders, grants, new RecordingEnforcer());

        Assert.Equal(ConfirmPaymentOutcome.AlreadyProcessed, outcome);
        var grant = await grants.FindAsync(Subscriber, CancellationToken.None);
        Assert.Equal(Now.AddDays(30), grant?.ExpiresAt);
    }

    [Fact]
    public async Task RenovacaoDeAssinaturaEstendeMesmoSemPedidoAberto()
    {
        var orders = new InMemoryOrders();
        var grants = new InMemoryGrants();
        var enforcer = new RecordingEnforcer();

        var grant = AccessGrant.NeverPaid(Subscriber);
        grant.Extend(PlanTerms.Of(Monthly), Now);
        grant.AttachSubscription(new Subscription("stub", "sub_42"));
        await grants.SaveAsync(grant, CancellationToken.None);

        var renewal = new PaymentEvent(PaymentEventKind.Settled, "pay_novo", Now.AddDays(30))
        {
            SubscriptionReference = "sub_42"
        };
        var outcome = await Confirm(renewal, orders, grants, enforcer);

        Assert.Equal(ConfirmPaymentOutcome.AccessGranted, outcome);
        var renewed = await grants.FindAsync(Subscriber, CancellationToken.None);
        Assert.Equal(Now.AddDays(60), renewed?.ExpiresAt);
    }

    [Fact]
    public async Task RenovacaoDeAssinanteDesconhecidoNaoLiberaNinguem()
    {
        var enforcer = new RecordingEnforcer();

        var renewal = new PaymentEvent(PaymentEventKind.Settled, "pay_x", Now)
        {
            SubscriptionReference = "sub_inexistente"
        };
        var outcome = await Confirm(renewal, new InMemoryOrders(), new InMemoryGrants(), enforcer);

        Assert.Equal(ConfirmPaymentOutcome.OrderNotFound, outcome);
        Assert.Empty(enforcer.Applied);
    }

    [Fact]
    public async Task EstornoCortaOAcessoNaHora()
    {
        var orders = new InMemoryOrders();
        var grants = new InMemoryGrants();
        var enforcer = new RecordingEnforcer();
        var order = Order.Open(Guid.NewGuid(), Subscriber, Monthly, "stub", Now);
        order.TrackAs("pay_1");
        await orders.SaveAsync(order, CancellationToken.None);
        await Confirm(new PaymentEvent(PaymentEventKind.Settled, "pay_1", Now), orders, grants, enforcer);

        await Confirm(new PaymentEvent(PaymentEventKind.Refunded, "pay_1", Now.AddDays(1)), orders, grants, enforcer);

        var grant = await grants.FindAsync(Subscriber, CancellationToken.None);
        Assert.False(grant!.IsActiveAt(Now.AddDays(2), TimeSpan.Zero));
        Assert.Contains((Subscriber, AccessDecision.Deny), enforcer.Applied);
    }

    [Fact]
    public async Task AssinaturaCanceladaMantemOAcessoAteOFimDoPeriodoPago()
    {
        var orders = new InMemoryOrders();
        var grants = new InMemoryGrants();
        var order = Order.Open(Guid.NewGuid(), Subscriber, Monthly, "stub", Now);
        order.TrackAs("pay_1");
        await orders.SaveAsync(order, CancellationToken.None);
        await Confirm(new PaymentEvent(PaymentEventKind.Settled, "pay_1", Now), orders, grants, new RecordingEnforcer());

        await Confirm(
            new PaymentEvent(PaymentEventKind.SubscriptionCanceled, "pay_1", Now.AddDays(5)),
            orders,
            grants,
            new RecordingEnforcer());

        var grant = await grants.FindAsync(Subscriber, CancellationToken.None);
        Assert.True(grant!.IsActiveAt(Now.AddDays(20), TimeSpan.Zero));
        Assert.Null(grant.Subscription);
    }

    private static Task<ConfirmPaymentOutcome> Confirm(
        PaymentEvent payment,
        InMemoryOrders orders,
        InMemoryGrants grants,
        RecordingEnforcer enforcer)
    {
        var provider = new StubProvider(payment);
        var useCase = new ConfirmPayment(provider, orders, grants, enforcer, new SequentialIdentifiers());

        return useCase.ExecuteAsync(AnyNotification, CancellationToken.None);
    }
}
