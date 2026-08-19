using Jellyfin.Plugin.Paywall.Configuration;
using Paywall.Application;
using Paywall.Application.Ports;
using Paywall.Domain;

namespace Jellyfin.Plugin.Paywall.Jellyfin;

/// <summary>
/// Traduz a configuração do plugin, que é detalhe de entrega, para as portas do núcleo.
/// Lê sempre a instância corrente, para que salvar no painel tenha efeito sem reiniciar.
/// </summary>
public sealed class PaywallConfigurationAdapter : IPaywallSettings, IPlanCatalog
{
    public TimeSpan GracePeriod => TimeSpan.FromDays(Math.Max(0, Settings.Current.GracePeriodDays));

    public IReadOnlyCollection<Plan> All => Settings.Current.Plans.Select(ToPlan).ToArray();

    public Plan? Find(string planId)
    {
        var entry = Settings.Current.Plans.FirstOrDefault(plan =>
            string.Equals(plan.Id, planId, StringComparison.OrdinalIgnoreCase));

        return entry is null ? null : ToPlan(entry);
    }

    private static Plan ToPlan(PlanEntry entry) => new(
        entry.Id,
        entry.Name,
        Money.Of(entry.PriceCents),
        entry.IsSubscription ? BillingMode.Recurring : BillingMode.OneTime,
        entry.DurationDays > 0 ? AccessDuration.OfDays(entry.DurationDays) : AccessDuration.Lifetime);
}

public sealed class PublicUrlWebhookEndpoints : IWebhookEndpoints
{
    public Uri For(string providerKey)
    {
        var baseUrl = Plugin.Instance?.Configuration.PublicBaseUrl;

        if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var parsed))
        {
            throw new MissingPublicUrlException();
        }

        return new Uri(parsed, $"/Paywall/Webhook/{providerKey}");
    }
}
