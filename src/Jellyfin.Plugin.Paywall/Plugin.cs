using Jellyfin.Plugin.Paywall.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Paywall;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        DataPath = Path.Combine(applicationPaths.DataPath, "paywall");
    }

    public static Plugin? Instance { get; private set; }

    public override string Name => "Paywall";

    public override Guid Id => Guid.Parse("6f1c9a3e-4b52-4d1a-9b3f-2c8d5e7a1b40");

    public override string Description => "Libera o acesso à biblioteca conforme pagamento ou assinatura.";

    public string DataPath { get; }

    public IEnumerable<PluginPageInfo> GetPages() =>
    [
        new PluginPageInfo
        {
            Name = Name,
            EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html"
        }
    ];
}
