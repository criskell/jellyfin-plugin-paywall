using Jellyfin.Data.Events.Users;
using MediaBrowser.Controller.Events;
using Microsoft.Extensions.Logging;
using Paywall.Application.UseCases;

namespace Jellyfin.Plugin.Paywall.Events;

public sealed class NewUserGate(ApplyCurrentAccess applyAccess, ILogger<NewUserGate> logger)
    : IEventConsumer<UserCreatedEventArgs>
{
    public async Task OnEvent(UserCreatedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        var decision = await applyAccess.ExecuteAsync(eventArgs.Argument.Id, CancellationToken.None)
            .ConfigureAwait(false);

        logger.LogInformation(
            "Paywall: usuário {User} criado e avaliado como {Decision}.",
            eventArgs.Argument.Username,
            decision?.ToString() ?? "isento");
    }
}
