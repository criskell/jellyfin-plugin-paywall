using Jellyfin.Plugin.Paywall.Jellyfin;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Paywall.Application.Payments;
using Paywall.Application.Ports;
using Paywall.Application.UseCases;
using Paywall.Infrastructure;
using Paywall.Infrastructure.Providers;
using Paywall.Infrastructure.Storage;

namespace Jellyfin.Plugin.Paywall;

/// <summary>
/// Raiz de composição: o único lugar que conhece implementações concretas.
/// </summary>
public sealed class PaywallServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<IClock, SystemClock>();
        serviceCollection.AddSingleton<IIdentifierFactory, GuidIdentifierFactory>();

        serviceCollection.AddSingleton<PaywallConfigurationAdapter>();
        serviceCollection.AddSingleton<IPaywallSettings>(s => s.GetRequiredService<PaywallConfigurationAdapter>());
        serviceCollection.AddSingleton<IPlanCatalog>(s => s.GetRequiredService<PaywallConfigurationAdapter>());
        serviceCollection.AddSingleton<IManualPixOptions>(s => s.GetRequiredService<PaywallConfigurationAdapter>());
        serviceCollection.AddSingleton<IAsaasOptions>(s => s.GetRequiredService<PaywallConfigurationAdapter>());
        serviceCollection.AddSingleton<IWebhookEndpoints, PublicUrlWebhookEndpoints>();

        serviceCollection.AddSingleton(_ => new PaywallDatabase(Plugin.Instance!.DataPath));
        serviceCollection.AddSingleton<IOrderRepository, SqliteOrderRepository>();
        serviceCollection.AddSingleton<IAccessGrantRepository, SqliteAccessGrantRepository>();

        serviceCollection.AddSingleton<IAccessEnforcer, JellyfinAccessEnforcer>();
        serviceCollection.AddSingleton<ISubscriberDirectory, JellyfinSubscriberDirectory>();

        serviceCollection.AddSingleton<IPaymentProvider, ManualPixProvider>();
        serviceCollection.AddHttpClient<AsaasPixProvider>();
        serviceCollection.AddSingleton<IPaymentProvider>(s => s.GetRequiredService<AsaasPixProvider>());
        serviceCollection.AddSingleton<IPaymentProviderRegistry, PaymentProviderRegistry>();

        serviceCollection.AddSingleton<StartCheckout>();
        serviceCollection.AddSingleton<ConfirmPayment>();
        serviceCollection.AddSingleton<SyncAccess>();
        serviceCollection.AddSingleton<GrantAccessManually>();
        serviceCollection.AddSingleton<RevokeAccess>();
        serviceCollection.AddSingleton<GetAccessStatus>();
        serviceCollection.AddSingleton<ListSubscribers>();
    }
}
