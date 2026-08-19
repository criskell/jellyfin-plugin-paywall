using MediaBrowser.Controller.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paywall.Application.UseCases;

namespace Jellyfin.Plugin.Paywall.Events;

public sealed class SessionAccessGate(
    ISessionManager sessionManager,
    ApplyCurrentAccess applyAccess,
    ILogger<SessionAccessGate> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        sessionManager.SessionStarted += OnSessionStarted;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        sessionManager.SessionStarted -= OnSessionStarted;
        return Task.CompletedTask;
    }

    private async void OnSessionStarted(object? sender, SessionEventArgs eventArgs)
    {
        var userId = eventArgs.SessionInfo.UserId;

        if (userId == Guid.Empty)
        {
            return;
        }

        try
        {
            await applyAccess.ExecuteAsync(userId, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception failure)
        {
            logger.LogError(failure, "Paywall: falha ao avaliar o acesso no início da sessão.");
        }
    }
}
