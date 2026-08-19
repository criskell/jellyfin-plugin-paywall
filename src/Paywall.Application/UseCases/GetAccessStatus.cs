using Paywall.Application.Payments;
using Paywall.Application.Ports;
using Paywall.Domain;

namespace Paywall.Application.UseCases;

public sealed record PaymentMethodView(string Key, string DisplayName);

public sealed record PlanView(string Id, string Name, decimal Price, string Currency, string BillingMode, string Access);

public sealed record AccessStatusView(
    bool HasAccess,
    string? PlanId,
    DateTimeOffset? ExpiresAt,
    bool IsLifetime,
    bool HasActiveSubscription,
    IReadOnlyCollection<PlanView> Plans,
    IReadOnlyCollection<PaymentMethodView> PaymentMethods);

/// <summary>Alimenta a tela que o usuário vê quando está sem acesso.</summary>
public sealed class GetAccessStatus(
    IAccessGrantRepository grants,
    IPlanCatalog plans,
    IPaymentProviderRegistry providers,
    IPaywallSettings settings,
    IClock clock)
{
    public async Task<AccessStatusView> ExecuteAsync(Guid userId, CancellationToken cancellationToken)
    {
        var grant = await grants.FindAsync(userId, cancellationToken).ConfigureAwait(false)
                    ?? AccessGrant.NeverPaid(userId);

        var hasAccess = grant.IsActiveAt(clock.UtcNow, settings.GracePeriod);

        return new AccessStatusView(
            hasAccess,
            grant.PlanId,
            grant.ExpiresAt,
            grant.PlanId is not null && grant.ExpiresAt is null,
            grant.SubscriptionReference is not null,
            plans.All.Select(Describe).ToArray(),
            providers.Available.Select(p => new PaymentMethodView(p.Key, p.DisplayName)).ToArray());
    }

    private static PlanView Describe(Plan plan) => new(
        plan.Id,
        plan.Name,
        plan.Price.Amount,
        plan.Price.Currency,
        plan.BillingMode == BillingMode.Recurring ? "assinatura" : "pagamento único",
        plan.Duration.ToString());
}
