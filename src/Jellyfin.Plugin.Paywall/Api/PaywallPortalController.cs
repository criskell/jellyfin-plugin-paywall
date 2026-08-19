using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.Paywall.Api;

[ApiController]
[Route("Paywall")]
public sealed class PaywallPortalController : ControllerBase
{
    private const string PageResource = "Jellyfin.Plugin.Paywall.Web.portal.html";

    [HttpGet("Portal")]
    [AllowAnonymous]
    [Produces("text/html")]
    public ActionResult Portal()
    {
        using var stream = typeof(PaywallPortalController).Assembly.GetManifestResourceStream(PageResource);

        if (stream is null)
        {
            return NotFound();
        }

        using var reader = new StreamReader(stream);

        return Content(reader.ReadToEnd(), "text/html; charset=utf-8");
    }
}
