using Jellyfin.Plugin.Paywall.Configuration;
using Paywall.Application;
using Paywall.Application.Ports;
using Paywall.Domain;

namespace Jellyfin.Plugin.Paywall.Jellyfin;

public sealed class PaywallConfigurationAdapter : IPaywallSettings, IPlanCatalog
{
    public TimeSpan GracePeriod => TimeSpan.FromDays(Math.Max(0, Settings.Current.GracePeriodDays));

    public IReadOnlyCollection<Plan> All =>
        Settings.Current.Plans.Select(TryToPlan).OfType<Plan>().ToArray();

    public Plan? Find(string planId)
    {
        var entry = Settings.Current.Plans.FirstOrDefault(plan =>
            string.Equals(plan.Id, planId, StringComparison.OrdinalIgnoreCase));

        return entry is null ? null : TryToPlan(entry);
    }

    private static Plan? TryToPlan(PlanEntry entry)
    {
        var describable = !string.IsNullOrWhiteSpace(entry.Id) && !string.IsNullOrWhiteSpace(entry.Name);
        var chargeable = entry.PriceCents >= 0 && (!entry.IsSubscription || entry.DurationDays > 0);

        return describable && chargeable ? ToPlan(entry) : null;
    }

    private static Plan ToPlan(PlanEntry entry) => new(
        entry.Id,
        entry.Name,
        Money.Of(entry.PriceCents),
        entry.IsSubscription ? BillingMode.Recurring : BillingMode.OneTime,
        entry.DurationDays > 0 ? AccessDuration.OfDays(entry.DurationDays) : AccessDuration.Lifetime);
}

public sealed class PublicPaywallUrls : IPaywallUrls
{
    public Uri Portal => Resolve("/Paywall/Portal");

    public Uri WebhookFor(string providerKey) => Resolve($"/Paywall/Webhook/{providerKey}");

    private static Uri Resolve(string path)
    {
        var baseUrl = Settings.Current.PublicBaseUrl;

        if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var parsed))
        {
            throw new MissingPublicUrlException();
        }

        return new Uri(parsed, path);
    }
}
