using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Paywall.Application;
using Paywall.Application.Payments;
using Paywall.Application.Ports;
using Paywall.Domain;

namespace Paywall.Infrastructure.Providers.Crypto;

public interface IBtcPayOptions
{
    /// <summary>Endereço da sua instância, por exemplo <c>https://btcpay.seudominio.com</c>.</summary>
    string? ServerUrl { get; }

    string? StoreId { get; }

    string? ApiKey { get; }

    string? WebhookSecret { get; }
}

/// <summary>
/// Bitcoin e Lightning pela sua própria instância de BTCPay Server: sem intermediário
/// custodiando o dinheiro e sem taxa de processamento.
/// </summary>
public sealed class BtcPayServerProvider(HttpClient http, IBtcPayOptions options, IClock clock)
    : IPaymentProvider
{
    public string Key => "btcpay";

    public string DisplayName => "Bitcoin e Lightning (BTCPay Server)";

    /// <summary>
    /// Cripto não tem débito automático: ninguém consegue puxar o pagamento do usuário todo mês.
    /// Assinatura aqui seria renovação manual, então o provedor só se oferece para cobrança avulsa.
    /// </summary>
    public IReadOnlyCollection<BillingMode> SupportedModes { get; } = [BillingMode.OneTime];

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(options.ServerUrl)
        && !string.IsNullOrWhiteSpace(options.StoreId)
        && !string.IsNullOrWhiteSpace(options.ApiKey)
        && !string.IsNullOrWhiteSpace(options.WebhookSecret);

    public async Task<CheckoutTicket> StartCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var body = new
        {
            amount = request.Plan.Price.Amount.ToString("0.00", CultureInfo.InvariantCulture),
            currency = request.Plan.Price.Currency,
            metadata = new { orderId = request.OrderId.ToString("D"), itemDesc = request.Plan.Name },
            checkout = new { redirectURL = request.ReturnUrl?.ToString() }
        };

        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"{options.ServerUrl!.TrimEnd('/')}/api/v1/stores/{options.StoreId}/invoices")
        {
            Content = JsonContent.Create(body)
        };
        message.Headers.TryAddWithoutValidation("Authorization", "token " + options.ApiKey);

        using var response = await http.SendAsync(message, cancellationToken).ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new PaywallException($"BTCPay recusou a fatura ({(int)response.StatusCode}): {payload}");
        }

        using var document = JsonDocument.Parse(payload);
        var invoice = document.RootElement;

        var invoiceId = Text(invoice, "id") ?? throw new PaywallException("BTCPay não devolveu a fatura criada.");

        return new CheckoutTicket(invoiceId, new PaymentInstructions
        {
            RedirectUrl = Uri.TryCreate(Text(invoice, "checkoutLink"), UriKind.Absolute, out var link) ? link : null,
            Message = "Abra a página de pagamento para escolher entre Lightning e on-chain.",
            ExpiresAt = ReadExpiry(invoice)
        });
    }

    public Task<PaymentEvent?> InterpretAsync(InboundNotification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var signature = notification.Headers.GetValueOrDefault("BTCPay-Sig");

        if (!WebhookSignature.MatchesSha256(notification.Body, options.WebhookSecret!, signature))
        {
            throw new PaywallException("Notificação do BTCPay com assinatura inválida.");
        }

        using var document = JsonDocument.Parse(notification.Body);
        var root = document.RootElement;

        if (MapEvent(Text(root, "type")) is not { } kind || Text(root, "invoiceId") is not { } invoiceId)
        {
            return Task.FromResult<PaymentEvent?>(null);
        }

        return Task.FromResult<PaymentEvent?>(new PaymentEvent(kind, invoiceId, clock.UtcNow)
        {
            OrderId = ReadOrderId(root)
        });
    }

    /// <summary>
    /// <c>InvoiceSettled</c> é o único que significa dinheiro confirmado: <c>InvoiceProcessing</c>
    /// ainda espera confirmação na rede e <c>InvoiceReceivedPayment</c> pode ser pagamento parcial.
    /// </summary>
    private static PaymentEventKind? MapEvent(string? type) => type switch
    {
        "InvoiceSettled" => PaymentEventKind.Settled,
        "InvoiceExpired" or "InvoiceInvalid" => PaymentEventKind.Failed,
        _ => null
    };

    private static Guid? ReadOrderId(JsonElement root)
    {
        if (root.TryGetProperty("metadata", out var metadata) && Guid.TryParse(Text(metadata, "orderId"), out var id))
        {
            return id;
        }

        return null;
    }

    private static DateTimeOffset? ReadExpiry(JsonElement invoice) =>
        invoice.TryGetProperty("expirationTime", out var value) && value.TryGetInt64(out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
