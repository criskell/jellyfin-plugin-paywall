using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Paywall.Application;
using Paywall.Application.Payments;
using Paywall.Application.Ports;
using Paywall.Domain;

namespace Paywall.Infrastructure.Providers.Crypto;

public interface IOpenNodeOptions
{
    string? ApiKey { get; }

    /// <summary>Ambiente de desenvolvimento da OpenNode, para testar sem dinheiro real.</summary>
    bool UseDevelopment { get; }
}

/// <summary>
/// Bitcoin pela OpenNode, hospedado. Cada cobrança nasce com fatura Lightning e endereço
/// on-chain, o que importa para mensalidade pequena: taxa on-chain comeria o valor.
/// </summary>
public sealed class OpenNodeProvider(HttpClient http, IOpenNodeOptions options, IClock clock) : IPaymentProvider
{
    public string Key => "opennode";

    public string DisplayName => "Bitcoin e Lightning (OpenNode)";

    public IReadOnlyCollection<BillingMode> SupportedModes { get; } = [BillingMode.OneTime];

    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.ApiKey);

    private string BaseUrl =>
        options.UseDevelopment ? "https://dev-api.opennode.com/v1" : "https://api.opennode.com/v1";

    public async Task<CheckoutTicket> StartCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var body = new
        {
            amount = request.Plan.Price.Amount.ToString("0.00", CultureInfo.InvariantCulture),
            currency = request.Plan.Price.Currency,
            description = request.Plan.Name,
            order_id = request.OrderId.ToString("D"),
            callback_url = request.WebhookUrl.ToString(),
            success_url = request.ReturnUrl?.ToString(),
            auto_settle = false
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, BaseUrl + "/charges")
        {
            Content = JsonContent.Create(body)
        };
        message.Headers.TryAddWithoutValidation("Authorization", options.ApiKey);

        using var response = await http.SendAsync(message, cancellationToken).ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new PaywallException($"OpenNode recusou a cobrança ({(int)response.StatusCode}): {payload}");
        }

        using var document = JsonDocument.Parse(payload);

        if (!document.RootElement.TryGetProperty("data", out var charge) || Text(charge, "id") is not { } chargeId)
        {
            throw new PaywallException("OpenNode não devolveu a cobrança criada.");
        }

        return new CheckoutTicket(chargeId, Describe(charge));
    }

    public Task<PaymentEvent?> InterpretAsync(InboundNotification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var fields = FormEncodedBody.Parse(notification.Body);

        if (fields.GetValueOrDefault("id") is not { Length: > 0 } chargeId)
        {
            return Task.FromResult<PaymentEvent?>(null);
        }

        // A OpenNode assina só o id da cobrança, usando a própria chave de API como segredo.
        var expected = WebhookSignature.Sha256Hex(chargeId, options.ApiKey!);

        if (!string.Equals(expected, fields.GetValueOrDefault("hashed_order"), StringComparison.OrdinalIgnoreCase))
        {
            throw new PaywallException("Notificação da OpenNode com assinatura inválida.");
        }

        if (MapStatus(fields.GetValueOrDefault("status")) is not { } kind)
        {
            return Task.FromResult<PaymentEvent?>(null);
        }

        return Task.FromResult<PaymentEvent?>(new PaymentEvent(kind, chargeId, clock.UtcNow)
        {
            OrderId = Guid.TryParse(fields.GetValueOrDefault("order_id"), out var orderId) ? orderId : null
        });
    }

    private static PaymentEventKind? MapStatus(string? status) => status switch
    {
        "paid" => PaymentEventKind.Settled,
        "expired" => PaymentEventKind.Failed,
        "refunded" => PaymentEventKind.Refunded,
        _ => null
    };

    /// <summary>
    /// Prefere a fatura Lightning como copia e cola; o endereço on-chain fica na mensagem,
    /// para quem quiser pagar pela cadeia principal.
    /// </summary>
    private static PaymentInstructions Describe(JsonElement charge)
    {
        var lightning = charge.TryGetProperty("lightning_invoice", out var invoice) ? Text(invoice, "payreq") : null;
        var onChain = charge.TryGetProperty("chain_invoice", out var chain) ? Text(chain, "address") : null;

        return new PaymentInstructions
        {
            CopyPasteCode = lightning ?? onChain,
            RedirectUrl = Uri.TryCreate(Text(charge, "hosted_checkout_url"), UriKind.Absolute, out var link)
                ? link
                : null,
            Message = lightning is not null && onChain is not null
                ? "O código acima é a fatura Lightning. Para pagar on-chain, use o endereço " + onChain + "."
                : null
        };
    }

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
