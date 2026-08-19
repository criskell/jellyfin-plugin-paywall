using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using Paywall.Application.UseCases;

namespace Jellyfin.Plugin.Paywall.Tasks;

public sealed class AccessSyncTask(SyncAccess syncAccess, ILogger<AccessSyncTask> logger) : IScheduledTask
{
    public string Name => "Paywall: revisar acessos";

    public string Key => "PaywallAccessSync";

    public string Description => "Compara os vencimentos com as permissões e ajusta quem pode assistir.";

    public string Category => "Paywall";

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        progress.Report(0);

        var report = await syncAccess.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Paywall: {Allowed} liberados, {Denied} bloqueados.", report.Allowed, report.Denied);

        progress.Report(100);
    }

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() =>
    [
        new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.IntervalTrigger,
            IntervalTicks = TimeSpan.FromHours(1).Ticks
        }
    ];
}
