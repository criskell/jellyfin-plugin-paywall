using System.Net.Http.Json;
using System.Text.Json;
using Paywall.Application;
using Paywall.Application.Payments;
using Paywall.Application.Ports;
using Paywall.Domain;

namespace Paywall.Infrastructure.Providers.Crypto;

public interface INowPaymentsOptions
{
    string? ApiKey { get; }

    /// <summary>Segredo de IPN, gerado no painel. Sem ele a notificação não é verificável.</summary>
    string? IpnSecret { get; }

    bool UseSandbox { get; }
}

/// <summary>
/// Centenas de moedas pela NOWPayments, com página de pagamento hospedada onde o usuário
/// escolhe em que cripto quer pagar.
/// </summary>
public sealed class NowPaymentsProvider(HttpClient http, INowPaymentsOptions options, IClock clock) : IPaymentProvider
{
    public string Key => "nowpayments";

    public string DisplayName => "Criptomoedas (NOWPayments)";

    public IReadOnlyCollection<BillingMode> SupportedModes { get; } = [BillingMode.OneTime];

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(options.ApiKey) && !string.IsNullOrWhiteSpace(options.IpnSecret);

    private string BaseUrl =>
        options.UseSandbox ? "https://api-sandbox.nowpayments.io/v1" : "https://api.nowpayments.io/v1";

    public async Task<CheckoutTicket> StartCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var body = new
        {
            price_amount = request.Plan.Price.Amount,
            price_currency = request.Plan.Price.Currency,
            order_id = request.OrderId.ToString("D"),
            order_description = request.Plan.Name,
            ipn_callback_url = request.WebhookUrl.ToString(),
            success_url = request.ReturnUrl?.ToString()
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, BaseUrl + "/invoice")
        {
            Content = JsonContent.Create(body)
        };
        message.Headers.TryAddWithoutValidation("x-api-key", options.ApiKey);

        using var response = await http.SendAsync(message, cancellationToken).ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new PaywallException($"NOWPayments recusou a fatura ({(int)response.StatusCode}): {payload}");
        }

        using var document = JsonDocument.Parse(payload);
        var invoice = document.RootElement;

        var invoiceId = Text(invoice, "id") ?? throw new PaywallException("NOWPayments não devolveu a fatura criada.");

        return new CheckoutTicket(invoiceId, new PaymentInstructions
        {
            RedirectUrl = Uri.TryCreate(Text(invoice, "invoice_url"), UriKind.Absolute, out var link) ? link : null,
            Message = "Abra a página de pagamento para escolher a moeda."
        });
    }

    public Task<PaymentEvent?> InterpretAsync(InboundNotification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var canonical = SortedJson.Canonicalize(notification.Body);
        var signature = notification.Headers.GetValueOrDefault("x-nowpayments-sig");

        if (!WebhookSignature.MatchesSha512(canonical, options.IpnSecret!, signature))
        {
            throw new PaywallException("Notificação da NOWPayments com assinatura inválida.");
        }

        using var document = JsonDocument.Parse(notification.Body);
        var root = document.RootElement;

        if (MapStatus(Text(root, "payment_status")) is not { } kind)
        {
            return Task.FromResult<PaymentEvent?>(null);
        }

        var reference = Text(root, "invoice_id") ?? Text(root, "payment_id");

        if (reference is null)
        {
            return Task.FromResult<PaymentEvent?>(null);
        }

        return Task.FromResult<PaymentEvent?>(new PaymentEvent(kind, reference, clock.UtcNow)
        {
            OrderId = Guid.TryParse(Text(root, "order_id"), out var orderId) ? orderId : null
        });
    }

    /// <summary>
    /// <c>partially_paid</c> fica de fora de propósito: pagamento incompleto não pode liberar
    /// acesso, e o usuário ainda tem prazo para completar.
    /// </summary>
    private static PaymentEventKind? MapStatus(string? status) => status switch
    {
        "confirmed" or "finished" => PaymentEventKind.Settled,
        "failed" or "expired" => PaymentEventKind.Failed,
        "refunded" => PaymentEventKind.Refunded,
        _ => null
    };

    /// <summary>Campos como <c>id</c> e <c>invoice_id</c> chegam ora como texto, ora como número.</summary>
    private static string? Text(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }
}
