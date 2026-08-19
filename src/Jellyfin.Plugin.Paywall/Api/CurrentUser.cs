using System.Security.Claims;

namespace Jellyfin.Plugin.Paywall.Api;

internal static class CurrentUser
{
    private const string JellyfinUserIdClaim = "Jellyfin-UserId";

    public static Guid? IdOf(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(JellyfinUserIdClaim), out var id) ? id : null;
}
