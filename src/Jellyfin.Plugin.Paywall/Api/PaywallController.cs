using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Paywall.Application;
using Paywall.Application.Payments;
using Paywall.Application.UseCases;

namespace Jellyfin.Plugin.Paywall.Api;

public sealed record StartCheckoutBody(string PlanId, string ProviderKey)
{
    public string? TaxId { get; init; }
}

/// <summary>
/// Adaptador humilde: traduz HTTP para caso de uso e de volta. Nenhuma regra de negócio aqui.
/// </summary>
[ApiController]
[Route("Paywall")]
[Produces("application/json")]
public sealed class PaywallController(
    GetAccessStatus accessStatus,
    StartCheckout startCheckout,
    MediaBrowser.Controller.Library.IUserManager userManager) : ControllerBase
{
    [HttpGet("Status")]
    [Authorize(Policy = "DefaultAuthorization")]
    public async Task<ActionResult<AccessStatusView>> GetStatus(CancellationToken cancellationToken)
    {
        if (CurrentUser.IdOf(User) is not { } userId)
        {
            return Unauthorized();
        }

        return await accessStatus.ExecuteAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    [HttpPost("Checkout")]
    [Authorize(Policy = "DefaultAuthorization")]
    public async Task<ActionResult<CheckoutTicket>> Checkout(
        [FromBody] StartCheckoutBody body,
        CancellationToken cancellationToken)
    {
        if (CurrentUser.IdOf(User) is not { } userId)
        {
            return Unauthorized();
        }

        var user = userManager.GetUserById(userId);

        if (user is null)
        {
            return Unauthorized();
        }

        var command = new StartCheckoutCommand(userId, user.Username, body.PlanId, body.ProviderKey)
        {
            TaxId = body.TaxId
        };

        try
        {
            return await startCheckout.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
        }
        catch (PaywallException failure)
        {
            return BadRequest(new { error = failure.Message });
        }
    }
}
