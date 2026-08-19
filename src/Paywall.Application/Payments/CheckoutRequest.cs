using Paywall.Domain;

namespace Paywall.Application.Payments;

public sealed record CheckoutRequest(Guid OrderId, Plan Plan, Payer Payer, Uri WebhookUrl)
{
    public Uri? ReturnUrl { get; init; }
}

public sealed record Payer(Guid UserId, string Name)
{
    public string? Email { get; init; }

    public string? TaxId { get; init; }
}
