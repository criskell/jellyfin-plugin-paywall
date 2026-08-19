using System.Security.Cryptography;
using System.Text;
using Paywall.Application;
using Paywall.Application.Payments;
using Paywall.Infrastructure.Providers.Crypto;
using Xunit;

namespace Paywall.Tests;

public class CryptoWebhookTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid OrderId = Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301");

    private static InboundNotification Notify(string key, string body, string header, string value) =>
        new(key,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [header] = value },
            new Dictionary<string, string>(),
            body);

    private static string HexSha256(string message, string secret) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(message)));

    private static string HexSha512(string message, string secret) =>
        Convert.ToHexStringLower(HMACSHA512.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(message)));

    public class BtcPay
    {
        private const string Secret = "segredo-do-webhook";

        private static readonly string SettledBody = """
            {"deliveryId":"d1","webhookId":"w1","type":"InvoiceSettled","invoiceId":"inv_9",
             "storeId":"store_1","metadata":{"orderId":"3f2504e0-4f89-11d3-9a0c-0305e82c3301"}}
            """;

        private static Task<PaymentEvent?> Interpret(string body, string signature)
        {
            var provider = new BtcPayServerProvider(new HttpClient(), new Options(), new FixedClock(Now));
            return provider.ReadPaymentEventAsync(Notify("btcpay", body, "BTCPay-Sig", signature), CancellationToken.None);
        }

        [Fact]
        public async Task FaturaLiquidadaLiberaOAcesso()
        {
            var payment = await Interpret(SettledBody, "sha256=" + HexSha256(SettledBody, Secret));

            Assert.Equal(PaymentEventKind.Settled, payment?.Kind);
            Assert.Equal("inv_9", payment?.ProviderReference);
            Assert.Equal(OrderId, payment?.OrderId);
        }

        [Fact]
        public async Task AssinaturaInvalidaERecusada()
        {
            await Assert.ThrowsAsync<PaywallException>(() => Interpret(SettledBody, "sha256=" + HexSha256(SettledBody, "outro")));
        }

        [Fact]
        public async Task AssinaturaMalFormadaERecusadaSemQuebrar()
        {
            await Assert.ThrowsAsync<PaywallException>(() => Interpret(SettledBody, "sha256=nao-e-hexadecimal"));
        }

        [Fact]
        public async Task AssinaturaVaziaERecusada()
        {
            await Assert.ThrowsAsync<PaywallException>(() => Interpret(SettledBody, string.Empty));
        }

        [Fact]
        public async Task FaturaEmProcessamentoNaoLiberaNada()
        {
            var body = SettledBody.Replace("InvoiceSettled", "InvoiceProcessing");

            Assert.Null(await Interpret(body, "sha256=" + HexSha256(body, Secret)));
        }

        private sealed class Options : IBtcPayOptions
        {
            public string? ServerUrl => "https://btcpay.exemplo.com";

            public string? StoreId => "store_1";

            public string? ApiKey => "chave";

            public string? WebhookSecret => Secret;
        }
    }

    public class NowPayments
    {
        private const string Secret = "segredo-ipn";

        private const string FinishedBody = """
            {
              "payment_status": "finished",
              "invoice_id": "123456",
              "order_id": "3f2504e0-4f89-11d3-9a0c-0305e82c3301",
              "price_amount": 19.9,
              "actually_paid": 19.9
            }
            """;

        private const string Canonical =
            """{"actually_paid":19.9,"invoice_id":"123456","order_id":"3f2504e0-4f89-11d3-9a0c-0305e82c3301","payment_status":"finished","price_amount":19.9}""";

        private static Task<PaymentEvent?> Interpret(string body, string signature)
        {
            var provider = new NowPaymentsProvider(new HttpClient(), new Options(), new FixedClock(Now));
            return provider.ReadPaymentEventAsync(
                Notify("nowpayments", body, "x-nowpayments-sig", signature),
                CancellationToken.None);
        }

        [Fact]
        public async Task PagamentoConcluidoLiberaOAcesso()
        {
            var payment = await Interpret(FinishedBody, HexSha512(Canonical, Secret));

            Assert.Equal(PaymentEventKind.Settled, payment?.Kind);
            Assert.Equal("123456", payment?.ProviderReference);
            Assert.Equal(OrderId, payment?.OrderId);
        }

        [Fact]
        public async Task AssinaturaSobreOCorpoCruERecusada()
        {
            await Assert.ThrowsAsync<PaywallException>(() => Interpret(FinishedBody, HexSha512(FinishedBody, Secret)));
        }

        [Fact]
        public async Task PagamentoParcialNaoLiberaNada()
        {
            var body = FinishedBody.Replace("finished", "partially_paid");
            var canonical = Canonical.Replace("finished", "partially_paid");

            Assert.Null(await Interpret(body, HexSha512(canonical, Secret)));
        }

        private sealed class Options : INowPaymentsOptions
        {
            public string? ApiKey => "chave";

            public string? IpnSecret => Secret;

            public bool UseSandbox => true;
        }
    }

    public class OpenNode
    {
        private const string ApiKeyValue = "chave-de-api";

        private static string Body(string status, string hashedOrder) =>
            "id=charge_7&status=" + status
            + "&order_id=3f2504e0-4f89-11d3-9a0c-0305e82c3301"
            + "&hashed_order=" + hashedOrder
            + "&description=Mensal+de+teste";

        private static Task<PaymentEvent?> Interpret(string body)
        {
            var provider = new OpenNodeProvider(new HttpClient(), new Options(), new FixedClock(Now));
            return provider.ReadPaymentEventAsync(
                Notify("opennode", body, "Content-Type", "application/x-www-form-urlencoded"),
                CancellationToken.None);
        }

        [Fact]
        public async Task CobrancaPagaLiberaOAcesso()
        {
            var payment = await Interpret(Body("paid", HexSha256("charge_7", ApiKeyValue)));

            Assert.Equal(PaymentEventKind.Settled, payment?.Kind);
            Assert.Equal("charge_7", payment?.ProviderReference);
            Assert.Equal(OrderId, payment?.OrderId);
        }

        [Fact]
        public async Task AssinaturaForjadaERecusada()
        {
            await Assert.ThrowsAsync<PaywallException>(() => Interpret(Body("paid", HexSha256("charge_7", "outra"))));
        }

        [Fact]
        public async Task CobrancaExpiradaNaoLiberaNada()
        {
            var payment = await Interpret(Body("expired", HexSha256("charge_7", ApiKeyValue)));

            Assert.Equal(PaymentEventKind.Failed, payment?.Kind);
        }

        private sealed class Options : IOpenNodeOptions
        {
            public string? ApiKey => ApiKeyValue;

            public bool UseDevelopment => true;
        }
    }
}
