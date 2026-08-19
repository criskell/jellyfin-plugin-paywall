using System.Security.Claims;

namespace Jellyfin.Plugin.Paywall.Api;

internal static class CurrentUser
{
    private const string JellyfinUserIdClaim = "Jellyfin-UserId";

    public static Guid? IdOf(ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(JellyfinUserIdClaim)
                  ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
