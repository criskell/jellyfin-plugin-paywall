using Paywall.Application;
using Paywall.Application.Payments;
using Paywall.Infrastructure.Providers;
using Xunit;

namespace Paywall.Tests;

public class AsaasWebhookTests
{
    private const string SecretToken = "token-secreto";

    private static readonly DateTimeOffset Now = new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);

    private const string ReceivedPayload = """
        {
          "event": "PAYMENT_RECEIVED",
          "payment": {
            "id": "pay_080225913252",
            "subscription": "sub_VXJBYgP2u0eO",
            "externalReference": "3f2504e0-4f89-11d3-9a0c-0305e82c3301",
            "value": 19.9,
            "status": "RECEIVED"
          }
        }
        """;

    [Fact]
    public async Task PagamentoRecebidoViraEventoDeLiquidacao()
    {
        var payment = await Interpret(ReceivedPayload, SecretToken);

        Assert.NotNull(payment);
        Assert.Equal(PaymentEventKind.Settled, payment.Kind);
        Assert.Equal("pay_080225913252", payment.ProviderReference);
        Assert.Equal("sub_VXJBYgP2u0eO", payment.SubscriptionReference);
        Assert.Equal(Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301"), payment.OrderId);
    }

    [Fact]
    public async Task NotificacaoComTokenErradoERecusada()
    {
        await Assert.ThrowsAsync<PaywallException>(() => Interpret(ReceivedPayload, "token-errado"));
    }

    [Fact]
    public async Task NotificacaoSemTokenERecusada()
    {
        await Assert.ThrowsAsync<PaywallException>(() => Interpret(ReceivedPayload, null));
    }

    [Fact]
    public async Task EventoIrrelevanteNaoViraNada()
    {
        var created = ReceivedPayload.Replace("PAYMENT_RECEIVED", "PAYMENT_CREATED");

        Assert.Null(await Interpret(created, SecretToken));
    }

    [Fact]
    public async Task EstornoViraEventoDeDevolucao()
    {
        var refunded = ReceivedPayload.Replace("PAYMENT_RECEIVED", "PAYMENT_REFUNDED");

        Assert.Equal(PaymentEventKind.Refunded, (await Interpret(refunded, SecretToken))?.Kind);
    }

    private static Task<PaymentEvent?> Interpret(string body, string? token)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (token is not null)
        {
            headers["asaas-access-token"] = token;
        }

        var provider = new AsaasPixProvider(new HttpClient(), new TestAsaasOptions(), new FixedClock(Now));
        var notification = new InboundNotification("asaas", headers, new Dictionary<string, string>(), body);

        return provider.ReadPaymentEventAsync(notification, CancellationToken.None);
    }

    private sealed class TestAsaasOptions : IAsaasOptions
    {
        public string? ApiKey => "chave";

        public string? WebhookToken => SecretToken;

        public bool UseSandbox => true;

        public string? DefaultTaxId => null;

        public string? ApplicationName => "testes";
    }
}
