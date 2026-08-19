using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Paywall.Configuration;

public enum EnforcementMode
{
    DisableAccount,

    RestrictLibraries
}

public class PluginConfiguration : BasePluginConfiguration
{
    public bool Enabled { get; set; }

    public string PublicBaseUrl { get; set; } = string.Empty;

    public int GracePeriodDays { get; set; } = 2;

    public EnforcementMode EnforcementMode { get; set; } = EnforcementMode.RestrictLibraries;

    public string[] FreeFolderIds { get; set; } = [];

    public string[] ExemptUserIds { get; set; } = [];

    public PlanEntry[] Plans { get; set; } =
    [
        new() { Id = "mensal", Name = "Mensal", PriceCents = 1990, DurationDays = 30 }
    ];

    public ManualPixSettings ManualPix { get; set; } = new();

    public AsaasSettings Asaas { get; set; } = new();

    public BtcPaySettings BtcPay { get; set; } = new();

    public NowPaymentsSettings NowPayments { get; set; } = new();

    public OpenNodeSettings OpenNode { get; set; } = new();
}

public class PlanEntry
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public long PriceCents { get; set; }

    public int DurationDays { get; set; }

    public bool IsSubscription { get; set; }
}

public class AsaasSettings
{
    public string ApiKey { get; set; } = string.Empty;

    public string WebhookToken { get; set; } = string.Empty;

    public bool UseSandbox { get; set; }

    public string PayerTaxId { get; set; } = string.Empty;
}

public class ManualPixSettings
{
    public string PixKey { get; set; } = string.Empty;

    public string PayeeName { get; set; } = string.Empty;

    public string PayeeCity { get; set; } = string.Empty;

    public string Instructions { get; set; } = string.Empty;
}

public class BtcPaySettings
{
    public string ServerUrl { get; set; } = string.Empty;

    public string StoreId { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string WebhookSecret { get; set; } = string.Empty;
}

public class NowPaymentsSettings
{
    public string ApiKey { get; set; } = string.Empty;

    public string IpnSecret { get; set; } = string.Empty;

    public bool UseSandbox { get; set; }
}

public class OpenNodeSettings
{
    public string ApiKey { get; set; } = string.Empty;

    public bool UseDevelopment { get; set; }
}
