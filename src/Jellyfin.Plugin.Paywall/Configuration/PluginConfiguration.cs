using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Paywall.Configuration;

public enum EnforcementMode
{
    /// <summary>Desabilita a conta: o usuário é desconectado e não consegue entrar.</summary>
    DisableAccount,

    /// <summary>Mantém o login, mas deixa visíveis só as pastas liberadas.</summary>
    RestrictLibraries
}

public class PluginConfiguration : BasePluginConfiguration
{
    public bool Enabled { get; set; }

    /// <summary>Endereço público do servidor, base da URL de webhook informada aos provedores.</summary>
    public string PublicBaseUrl { get; set; } = string.Empty;

    public int GracePeriodDays { get; set; } = 2;

    public EnforcementMode EnforcementMode { get; set; } = EnforcementMode.RestrictLibraries;

    /// <summary>Pastas que continuam visíveis para quem não pagou.</summary>
    public string[] FreeFolderIds { get; set; } = [];

    /// <summary>Contas que o paywall nunca toca, além dos administradores.</summary>
    public string[] ExemptUserIds { get; set; } = [];

    public PlanEntry[] Plans { get; set; } =
    [
        new() { Id = "mensal", Name = "Mensal", PriceCents = 1990, DurationDays = 30 }
    ];

    public ManualPixSettings ManualPix { get; set; } = new();

    public AsaasSettings Asaas { get; set; } = new();
}

public class PlanEntry
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public long PriceCents { get; set; }

    /// <summary>Zero significa acesso vitalício, válido apenas para pagamento único.</summary>
    public int DurationDays { get; set; }

    public bool IsSubscription { get; set; }
}

public class AsaasSettings
{
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Token que o Asaas devolve no header das notificações. Sem ele o webhook é recusado.</summary>
    public string WebhookToken { get; set; } = string.Empty;

    public bool UseSandbox { get; set; }
}

public class ManualPixSettings
{
    public string PixKey { get; set; } = string.Empty;

    public string PayeeName { get; set; } = string.Empty;

    public string PayeeCity { get; set; } = string.Empty;

    public string Instructions { get; set; } = string.Empty;
}
