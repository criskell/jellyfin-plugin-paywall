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
        var exempt = ExemptIds;

        var subscribers = userManager.GetUsers()
            .Where(user => !IsAdministrator(user.Id))
            .Where(user => !exempt.Contains(user.Id))
            .Select(user => new Subscriber(user.Id, user.Username))
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<Subscriber>>(subscribers);
    }

    public Task<bool> IsSubjectAsync(Guid userId, CancellationToken cancellationToken)
    {
        var subject = userManager.GetUserById(userId) is not null
                      && !IsAdministrator(userId)
                      && !ExemptIds.Contains(userId);

        return Task.FromResult(subject);
    }

    /// <summary>Aceita id com ou sem hífen, já que painel e arquivo de configuração divergem.</summary>
    private static HashSet<Guid> ExemptIds =>
        (Plugin.Instance?.Configuration.ExemptUserIds ?? [])
        .Select(id => Guid.TryParse(id, out var parsed) ? parsed : Guid.Empty)
        .Where(id => id != Guid.Empty)
        .ToHashSet();

    private bool IsAdministrator(Guid userId)
    {
        var user = userManager.GetUserById(userId);

        return user is not null && userManager.GetUserDto(user).Policy?.IsAdministrator == true;
    }
}
