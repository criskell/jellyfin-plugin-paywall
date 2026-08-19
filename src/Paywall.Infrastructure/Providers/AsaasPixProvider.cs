using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Paywall.Application;
using Paywall.Application.Payments;
using Paywall.Application.Ports;
using Paywall.Domain;

namespace Paywall.Infrastructure.Providers;

public interface IAsaasOptions
{
    string? ApiKey { get; }

    /// <summary>Token exigido no header <c>asaas-access-token</c> das notificações.</summary>
    string? WebhookToken { get; }

    bool UseSandbox { get; }

    /// <summary>
    /// CPF ou CNPJ usado quando o pagador não informa o próprio. O Asaas exige o documento
    /// para emitir cobrança, e num servidor doméstico raramente vale pedir isso a cada usuário.
    /// </summary>
    string? DefaultTaxId { get; }

    string? ApplicationName { get; }
}

/// <summary>
/// Pix pelo Asaas, tanto cobrança avulsa quanto assinatura recorrente. Confirmação chega
/// por webhook, então a liberação é automática.
/// </summary>
public sealed class AsaasPixProvider(HttpClient http, IAsaasOptions options, IClock clock)
    : IPaymentProvider, ISupportsSubscriptionCancellation
{
    public string Key => "asaas";

    public string DisplayName => "Pix via Asaas";

    public IReadOnlyCollection<BillingMode> SupportedModes { get; } = [BillingMode.OneTime, BillingMode.Recurring];

    /// <summary>
    /// Sem token de webhook qualquer um poderia forjar um pagamento aprovado, então a
    /// integração só é oferecida quando os dois segredos estão configurados.
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(options.ApiKey) && !string.IsNullOrWhiteSpace(options.WebhookToken);

    private string BaseUrl => options.UseSandbox ? "https://api-sandbox.asaas.com/v3" : "https://api.asaas.com/v3";

    public async Task<CheckoutTicket> StartCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var customerId = await EnsureCustomerAsync(request.Payer, cancellationToken).ConfigureAwait(false);

        return request.Plan.BillingMode == BillingMode.Recurring
            ? await StartSubscriptionAsync(request, customerId, cancellationToken).ConfigureAwait(false)
            : await StartSingleChargeAsync(request, customerId, cancellationToken).ConfigureAwait(false);
    }

    public Task<PaymentEvent?> InterpretAsync(InboundNotification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        RejectForgedNotification(notification);

        using var document = JsonDocument.Parse(notification.Body);
        var root = document.RootElement;

        if (!root.TryGetProperty("payment", out var payment)
            || MapEvent(Text(root, "event")) is not { } kind
            || Text(payment, "id") is not { } paymentId)
        {
            return Task.FromResult<PaymentEvent?>(null);
        }

        // A hora vem sem fuso declarado; usar o relógio local evita adiar ou antecipar vencimento.
        return Task.FromResult<PaymentEvent?>(new PaymentEvent(kind, paymentId, clock.UtcNow)
        {
            OrderId = Guid.TryParse(Text(payment, "externalReference"), out var orderId) ? orderId : null,
            SubscriptionReference = Text(payment, "subscription")
        });
    }

    public async Task CancelSubscriptionAsync(string subscriptionReference, CancellationToken cancellationToken)
    {
        await SendAsync(HttpMethod.Delete, $"/subscriptions/{subscriptionReference}", null, cancellationToken)
            .ConfigureAwait(false);
    }

    private void RejectForgedNotification(InboundNotification notification)
    {
        var received = notification.Headers.GetValueOrDefault("asaas-access-token");

        if (!string.Equals(received, options.WebhookToken, StringComparison.Ordinal))
        {
            throw new PaywallException("Notificação do Asaas com token inválido.");
        }
    }

    private static PaymentEventKind? MapEvent(string? name) => name switch
    {
        "PAYMENT_CONFIRMED" or "PAYMENT_RECEIVED" => PaymentEventKind.Settled,
        "PAYMENT_REFUNDED" or "PAYMENT_CHARGEBACK_REQUESTED" => PaymentEventKind.Refunded,
        "PAYMENT_OVERDUE" or "PAYMENT_DELETED" => PaymentEventKind.Failed,
        _ => null
    };

    /// <summary>
    /// O Asaas cobra por cliente cadastrado, então o usuário do Jellyfin é procurado pela
    /// referência externa antes de criar outro.
    /// </summary>
    private async Task<string> EnsureCustomerAsync(Payer payer, CancellationToken cancellationToken)
    {
        var reference = payer.UserId.ToString("N");

        var existing = await SendAsync(
            HttpMethod.Get,
            $"/customers?externalReference={reference}",
            null,
            cancellationToken).ConfigureAwait(false);

        if (existing.TryGetProperty("data", out var found)
            && found.GetArrayLength() > 0
            && Text(found[0], "id") is { } knownId)
        {
            return knownId;
        }

        var taxId = Blank(payer.TaxId) ?? Blank(options.DefaultTaxId);

        if (taxId is null)
        {
            throw new PaywallException(
                "O Asaas exige CPF ou CNPJ. Preencha o documento padrão na configuração do plugin.");
        }

        var created = await SendAsync(
            HttpMethod.Post,
            "/customers",
            new { name = payer.Name, cpfCnpj = taxId, email = payer.Email, externalReference = reference },
            cancellationToken).ConfigureAwait(false);

        return Text(created, "id") ?? throw new PaywallException("O Asaas não devolveu o cliente criado.");
    }

    private async Task<CheckoutTicket> StartSingleChargeAsync(
        CheckoutRequest request,
        string customerId,
        CancellationToken cancellationToken)
    {
        var created = await SendAsync(
            HttpMethod.Post,
            "/payments",
            new
            {
                customer = customerId,
                billingType = "PIX",
                value = request.Plan.Price.Amount,
                dueDate = Today,
                description = request.Plan.Name,
                externalReference = request.OrderId.ToString("D")
            },
            cancellationToken).ConfigureAwait(false);

        var paymentId = Text(created, "id") ?? throw new PaywallException("O Asaas não devolveu a cobrança criada.");

        return new CheckoutTicket(paymentId, await ReadQrCodeAsync(paymentId, cancellationToken).ConfigureAwait(false));
    }

    private async Task<CheckoutTicket> StartSubscriptionAsync(
        CheckoutRequest request,
        string customerId,
        CancellationToken cancellationToken)
    {
        var created = await SendAsync(
            HttpMethod.Post,
            "/subscriptions",
            new
            {
                customer = customerId,
                billingType = "PIX",
                value = request.Plan.Price.Amount,
                nextDueDate = Today,
                cycle = ToCycle(request.Plan.Duration),
                description = request.Plan.Name,
                externalReference = request.OrderId.ToString("D")
            },
            cancellationToken).ConfigureAwait(false);

        var subscriptionId = Text(created, "id")
                             ?? throw new PaywallException("O Asaas não devolveu a assinatura criada.");

        var firstCharge = await FindFirstChargeAsync(subscriptionId, cancellationToken).ConfigureAwait(false);

        if (firstCharge is null)
        {
            // A recorrência gera a primeira cobrança logo depois de criada, não junto.
            return new CheckoutTicket(subscriptionId, new PaymentInstructions
            {
                Message = "Assinatura criada. O Pix da primeira mensalidade aparece em instantes."
            });
        }

        return new CheckoutTicket(
            firstCharge,
            await ReadQrCodeAsync(firstCharge, cancellationToken).ConfigureAwait(false));
    }

    private async Task<string?> FindFirstChargeAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        var charges = await SendAsync(
            HttpMethod.Get,
            $"/subscriptions/{subscriptionId}/payments",
            null,
            cancellationToken).ConfigureAwait(false);

        if (!charges.TryGetProperty("data", out var data) || data.GetArrayLength() == 0)
        {
            return null;
        }

        return Text(data[0], "id");
    }

    private async Task<PaymentInstructions> ReadQrCodeAsync(string paymentId, CancellationToken cancellationToken)
    {
        var qrCode = await SendAsync(HttpMethod.Get, $"/payments/{paymentId}/pixQrCode", null, cancellationToken)
            .ConfigureAwait(false);

        var image = Text(qrCode, "encodedImage");

        return new PaymentInstructions
        {
            CopyPasteCode = Text(qrCode, "payload"),
            QrCodeImage = image is null ? null : "data:image/png;base64," + image,
            ExpiresAt = DateTimeOffset.TryParse(
                Text(qrCode, "expirationDate"),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var expiry)
                ? expiry
                : null
        };
    }

    private static string ToCycle(AccessDuration duration) => duration.Days switch
    {
        <= 7 => "WEEKLY",
        <= 15 => "BIWEEKLY",
        <= 31 => "MONTHLY",
        <= 92 => "QUARTERLY",
        <= 184 => "SEMIANNUALLY",
        _ => "YEARLY"
    };

    private string Today => clock.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private async Task<JsonElement> SendAsync(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(method, BaseUrl + path);
        message.Headers.TryAddWithoutValidation("access_token", options.ApiKey);
        message.Headers.TryAddWithoutValidation("User-Agent", options.ApplicationName ?? "jellyfin-paywall");

        if (body is not null)
        {
            message.Content = JsonContent.Create(body);
        }

        using var response = await http.SendAsync(message, cancellationToken).ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new PaywallException($"Asaas recusou a chamada ({(int)response.StatusCode}): {Describe(payload)}");
        }

        using var document = JsonDocument.Parse(payload);
        return document.RootElement.Clone();
    }

    private static string Describe(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);

            if (document.RootElement.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0)
            {
                return Text(errors[0], "description") ?? payload;
            }
        }
        catch (JsonException)
        {
            // Resposta que não é JSON já é a própria explicação.
        }

        return payload;
    }
}
