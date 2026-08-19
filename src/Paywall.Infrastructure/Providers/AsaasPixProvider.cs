using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Paywall.Application;
using Paywall.Application.Payments;
using Paywall.Application.Ports;
using Paywall.Infrastructure.Providers.Crypto;
using Paywall.Domain;

namespace Paywall.Infrastructure.Providers;

public interface IAsaasOptions
{
    string? ApiKey { get; }

    string? WebhookToken { get; }

    bool UseSandbox { get; }

    string? DefaultTaxId { get; }

    string? ApplicationName { get; }
}

public sealed class AsaasPixProvider(HttpClient http, IAsaasOptions options, IClock clock)
    : IPaymentProvider, ISupportsSubscriptionCancellation
{
    public string Key => "asaas";

    public string DisplayName => "Pix via Asaas";

    public IReadOnlyCollection<BillingMode> SupportedModes { get; } = [BillingMode.OneTime, BillingMode.Recurring];

    public bool IsConfigured => HasApiKey && CanVerifyNotifications;

    private bool HasApiKey => !string.IsNullOrWhiteSpace(options.ApiKey);

    private bool CanVerifyNotifications => !string.IsNullOrWhiteSpace(options.WebhookToken);

    private string BaseUrl => options.UseSandbox ? "https://api-sandbox.asaas.com/v3" : "https://api.asaas.com/v3";

    public async Task<CheckoutTicket> StartCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var customerId = await EnsureCustomerAsync(request.Payer, cancellationToken).ConfigureAwait(false);

        return request.Plan.BillingMode == BillingMode.Recurring
            ? await StartSubscriptionAsync(request, customerId, cancellationToken).ConfigureAwait(false)
            : await StartSingleChargeAsync(request, customerId, cancellationToken).ConfigureAwait(false);
    }

    public Task<PaymentEvent?> ReadPaymentEventAsync(InboundNotification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        RejectForgedNotification(notification);

        using var document = JsonDocument.Parse(notification.Body);
        var root = document.RootElement;

        var name = Text(root, "event");

        return Task.FromResult(ReadSubscriptionEnded(root, name) ?? ReadPayment(root, name));
    }

    private PaymentEvent? ReadSubscriptionEnded(JsonElement root, string? name)
    {
        if (name is not ("SUBSCRIPTION_DELETED" or "SUBSCRIPTION_INACTIVATED")
            || !root.TryGetProperty("subscription", out var subscription)
            || Text(subscription, "id") is not { } subscriptionId)
        {
            return null;
        }

        return new PaymentEvent(PaymentEventKind.SubscriptionCanceled, subscriptionId, clock.UtcNow)
        {
            SubscriptionReference = subscriptionId
        };
    }

    private PaymentEvent? ReadPayment(JsonElement root, string? name)
    {
        if (!root.TryGetProperty("payment", out var payment)
            || MapEvent(name) is not { } kind
            || Text(payment, "id") is not { } paymentId)
        {
            return null;
        }

        return new PaymentEvent(kind, paymentId, clock.UtcNow)
        {
            OrderId = Guid.TryParse(Text(payment, "externalReference"), out var orderId) ? orderId : null,
            SubscriptionReference = Text(payment, "subscription")
        };
    }

    public async Task CancelSubscriptionAsync(string subscriptionReference, CancellationToken cancellationToken)
    {
        await SendAsync(HttpMethod.Delete, $"/subscriptions/{subscriptionReference}", null, cancellationToken)
            .ConfigureAwait(false);
    }

    private void RejectForgedNotification(InboundNotification notification)
    {
        var received = notification.Headers.GetValueOrDefault("asaas-access-token");

        if (!WebhookSignature.SecretsMatch(options.WebhookToken!, received))
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

    private async Task<string> EnsureCustomerAsync(Payer payer, CancellationToken cancellationToken)
    {
        var reference = payer.UserId.ToString("N");

        return await FindCustomerAsync(reference, cancellationToken).ConfigureAwait(false)
               ?? await CreateCustomerAsync(payer, reference, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> FindCustomerAsync(string reference, CancellationToken cancellationToken)
    {
        var existing = await SendAsync(
            HttpMethod.Get,
            $"/customers?externalReference={reference}",
            null,
            cancellationToken).ConfigureAwait(false);

        return existing.TryGetProperty("data", out var found) && found.GetArrayLength() > 0
            ? Text(found[0], "id")
            : null;
    }

    private async Task<string> CreateCustomerAsync(
        Payer payer,
        string reference,
        CancellationToken cancellationToken)
    {
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
            throw new PaywallException(
                $"Asaas recusou a chamada ({(int)response.StatusCode}): {DescribeFailure(payload)}");
        }

        using var document = JsonDocument.Parse(payload);
        return document.RootElement.Clone();
    }

    private static string DescribeFailure(string payload) => TryReadFirstError(payload) ?? payload;

    private static string? TryReadFirstError(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);

            return document.RootElement.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0
                ? Text(errors[0], "description")
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
