using Jellyfin.Plugin.Paywall.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Users;
using Microsoft.Extensions.Logging;
using Paywall.Application.Ports;

namespace Jellyfin.Plugin.Paywall.Jellyfin;

public sealed class JellyfinAccessEnforcer(IUserManager userManager, ILogger<JellyfinAccessEnforcer> logger)
    : IAccessEnforcer
{
    public async Task ApplyAsync(Guid userId, AccessDecision decision, CancellationToken cancellationToken)
    {
        var configuration = Plugin.Instance?.Configuration;

        if (configuration is null || !configuration.Enabled)
        {
            return;
        }

        var user = userManager.GetUserById(userId);

        if (user is null)
        {
            return;
        }

        var policy = userManager.GetUserDto(user).Policy;

        if (policy is null || policy.IsAdministrator)
        {
            return;
        }

        var deny = decision == AccessDecision.Deny;

        var changed = configuration.EnforcementMode switch
        {
            EnforcementMode.DisableAccount => LockAccount(policy, deny),
            _ => RestrictLibraries(policy, deny, ParseFolders(configuration.FreeFolderIds))
        };

        if (!changed)
        {
            return;
        }

        await userManager.UpdatePolicyAsync(userId, policy).ConfigureAwait(false);
        logger.LogInformation("Paywall: acesso de {User} agora é {Decision}.", user.Username, decision);
    }

    private static bool LockAccount(UserPolicy policy, bool deny)
    {
        if (policy.IsDisabled == deny)
        {
            return false;
        }

        policy.IsDisabled = deny;
        return true;
    }

    private static Guid[] ParseFolders(string[] configuredIds) =>
        configuredIds.Select(id => Guid.TryParse(id, out var parsed) ? parsed : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToArray();

    private static bool RestrictLibraries(UserPolicy policy, bool deny, Guid[] freeFolderIds)
    {
        var seesEverything = !deny;
        Guid[] allowedFolders = deny ? freeFolderIds : [];

        if (policy.EnableAllFolders == seesEverything && policy.EnabledFolders.SequenceEqual(allowedFolders))
        {
            return false;
        }

        policy.EnableAllFolders = seesEverything;
        policy.EnabledFolders = allowedFolders;

        return true;
    }
}
