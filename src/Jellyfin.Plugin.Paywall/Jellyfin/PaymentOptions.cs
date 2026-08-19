using Jellyfin.Plugin.Paywall.Configuration;
using Paywall.Infrastructure.Providers;
using Paywall.Infrastructure.Providers.Crypto;

namespace Jellyfin.Plugin.Paywall.Jellyfin;

internal static class Settings
{
    public static PluginConfiguration Current => Plugin.Instance?.Configuration ?? new PluginConfiguration();

    public static string? Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

public sealed class ManualPixOptions : IManualPixOptions
{
    public string? PixKey => Settings.Blank(Settings.Current.ManualPix.PixKey);

    public string? PayeeName => Settings.Blank(Settings.Current.ManualPix.PayeeName);

    public string? PayeeCity => Settings.Blank(Settings.Current.ManualPix.PayeeCity);

    public string? Instructions => Settings.Blank(Settings.Current.ManualPix.Instructions);
}

public sealed class AsaasOptions : IAsaasOptions
{
    public string? ApiKey => Settings.Blank(Settings.Current.Asaas.ApiKey);

    public string? WebhookToken => Settings.Blank(Settings.Current.Asaas.WebhookToken);

    public bool UseSandbox => Settings.Current.Asaas.UseSandbox;

    public string? DefaultTaxId => Settings.Blank(Settings.Current.Asaas.PayerTaxId);

    public string? ApplicationName => "jellyfin-paywall";
}

public sealed class BtcPayOptions : IBtcPayOptions
{
    public string? ServerUrl => Settings.Blank(Settings.Current.BtcPay.ServerUrl);

    public string? StoreId => Settings.Blank(Settings.Current.BtcPay.StoreId);

    public string? ApiKey => Settings.Blank(Settings.Current.BtcPay.ApiKey);

    public string? WebhookSecret => Settings.Blank(Settings.Current.BtcPay.WebhookSecret);
}

public sealed class NowPaymentsOptions : INowPaymentsOptions
{
    public string? ApiKey => Settings.Blank(Settings.Current.NowPayments.ApiKey);

    public string? IpnSecret => Settings.Blank(Settings.Current.NowPayments.IpnSecret);

    public bool UseSandbox => Settings.Current.NowPayments.UseSandbox;
}

public sealed class OpenNodeOptions : IOpenNodeOptions
{
    public string? ApiKey => Settings.Blank(Settings.Current.OpenNode.ApiKey);

    public bool UseDevelopment => Settings.Current.OpenNode.UseDevelopment;
}
