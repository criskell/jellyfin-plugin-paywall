using MediaBrowser.Controller.Library;
using Paywall.Application.Ports;

namespace Jellyfin.Plugin.Paywall.Jellyfin;

/// <summary>
/// Quem está sujeito ao paywall. Administradores e isentos ficam fora para ninguém
/// se trancar para fora do próprio servidor.
/// </summary>
public sealed class JellyfinSubscriberDirectory(IUserManager userManager) : ISubscriberDirectory
{
    public Task<IReadOnlyCollection<Subscriber>> ListAsync(CancellationToken cancellationToken)
    {
        var exempt = Plugin.Instance?.Configuration.ExemptUserIds ?? [];

        var subscribers = userManager.GetUsers()
            .Where(user => userManager.GetUserDto(user).Policy?.IsAdministrator != true)
            .Where(user => !exempt.Contains(user.Id.ToString("N"), StringComparer.OrdinalIgnoreCase))
            .Select(user => new Subscriber(user.Id, user.Username))
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<Subscriber>>(subscribers);
    }
}
