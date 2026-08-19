using Jellyfin.Data.Events.Users;
using Jellyfin.Plugin.Paywall.Api;
using Jellyfin.Plugin.Paywall.Events;
using Jellyfin.Plugin.Paywall.Jellyfin;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Paywall.Application.Payments;
using Paywall.Application.Ports;
using Paywall.Application.UseCases;
using Paywall.Infrastructure;
using Paywall.Infrastructure.Providers;
using Paywall.Infrastructure.Providers.Crypto;
using Paywall.Infrastructure.Storage;

namespace Jellyfin.Plugin.Paywall;

public sealed class PaywallServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<IClock, SystemClock>();
        serviceCollection.AddSingleton<IIdentifierFactory, GuidIdentifierFactory>();

        serviceCollection.AddSingleton<PaywallConfigurationAdapter>();
        serviceCollection.AddSingleton<IPaywallSettings>(s => s.GetRequiredService<PaywallConfigurationAdapter>());
        serviceCollection.AddSingleton<IPlanCatalog>(s => s.GetRequiredService<PaywallConfigurationAdapter>());
        serviceCollection.AddSingleton<IManualPixOptions, ManualPixOptions>();
        serviceCollection.AddSingleton<IAsaasOptions, AsaasOptions>();
        serviceCollection.AddSingleton<IBtcPayOptions, BtcPayOptions>();
        serviceCollection.AddSingleton<INowPaymentsOptions, NowPaymentsOptions>();
        serviceCollection.AddSingleton<IOpenNodeOptions, OpenNodeOptions>();
        serviceCollection.AddSingleton<IPaywallUrls, PublicPaywallUrls>();

        serviceCollection.AddSingleton(_ => new PaywallDatabase(Plugin.Instance!.DataPath));
        serviceCollection.AddSingleton<IOrderRepository, SqliteOrderRepository>();
        serviceCollection.AddSingleton<IAccessGrantRepository, SqliteAccessGrantRepository>();

        serviceCollection.AddSingleton<IAccessEnforcer, JellyfinAccessEnforcer>();
        serviceCollection.AddSingleton<ISubscriberDirectory, JellyfinSubscriberDirectory>();

        serviceCollection.AddSingleton<IPaymentProvider, ManualPixProvider>();
        serviceCollection.AddHttpClient<AsaasPixProvider>();
        serviceCollection.AddSingleton<IPaymentProvider>(s => s.GetRequiredService<AsaasPixProvider>());
        serviceCollection.AddHttpClient<BtcPayServerProvider>();
        serviceCollection.AddSingleton<IPaymentProvider>(s => s.GetRequiredService<BtcPayServerProvider>());
        serviceCollection.AddHttpClient<OpenNodeProvider>();
        serviceCollection.AddSingleton<IPaymentProvider>(s => s.GetRequiredService<OpenNodeProvider>());
        serviceCollection.AddHttpClient<NowPaymentsProvider>();
        serviceCollection.AddSingleton<IPaymentProvider>(s => s.GetRequiredService<NowPaymentsProvider>());
        serviceCollection.AddSingleton<IPaymentProviderRegistry, PaymentProviderRegistry>();

        serviceCollection.AddSingleton<ApplyCurrentAccess>();
        serviceCollection.AddSingleton<StartCheckout>();
        serviceCollection.AddSingleton<ConfirmPayment>();
        serviceCollection.AddSingleton<PaymentNotificationGate>();
        serviceCollection.AddSingleton<SyncAccess>();
        serviceCollection.AddSingleton<GrantAccessManually>();
        serviceCollection.AddSingleton<RevokeAccess>();
        serviceCollection.AddSingleton<GetAccessStatus>();
        serviceCollection.AddSingleton<ListSubscribers>();

        serviceCollection.AddSingleton<IEventConsumer<UserCreatedEventArgs>, NewUserGate>();
        serviceCollection.AddHostedService<SessionAccessGate>();
    }
}
