using Paywall.Application.Payments;
using Paywall.Application.Ports;
using Paywall.Domain;

namespace Paywall.Application.UseCases;

public sealed record StartCheckoutCommand(Guid UserId, string UserName, string PlanId, string ProviderKey)
{
    public string? Email { get; init; }

    public string? TaxId { get; init; }
}

public sealed class StartCheckout(
    IPlanCatalog plans,
    IPaymentProviderRegistry providers,
    IOrderRepository orders,
    IPaywallUrls urls,
    IClock clock,
    IIdentifierFactory identifiers)
{
    public async Task<CheckoutTicket> ExecuteAsync(StartCheckoutCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var plan = plans.Find(command.PlanId) ?? throw new PlanNotFoundException(command.PlanId);
        var provider = providers.Resolve(command.ProviderKey);

        if (!provider.SupportedModes.Contains(plan.BillingMode))
        {
            throw new UnsupportedBillingModeException(provider.Key, plan.BillingMode.ToString());
        }

        var order = Order.Open(identifiers.NewId(), command.UserId, plan, provider.Key, clock.UtcNow);
        await orders.SaveAsync(order, cancellationToken).ConfigureAwait(false);

        var payer = new Payer(command.UserId, command.UserName) { Email = command.Email, TaxId = command.TaxId };
        var request = new CheckoutRequest(order.Id, plan, payer, urls.WebhookFor(provider.Key))
        {
            ReturnUrl = urls.Portal
        };

        var ticket = await provider.StartCheckoutAsync(request, cancellationToken).ConfigureAwait(false);

        order.TrackAs(ticket.ProviderReference);
        await orders.SaveAsync(order, cancellationToken).ConfigureAwait(false);

        return ticket;
    }
}
