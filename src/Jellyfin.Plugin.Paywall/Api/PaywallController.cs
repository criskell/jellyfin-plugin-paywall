using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Paywall.Application;
using Paywall.Application.Payments;
using Paywall.Application.UseCases;

namespace Jellyfin.Plugin.Paywall.Api;

public sealed record StartCheckoutBody(string PlanId, string ProviderKey);

public sealed record CheckoutView(
    string? CopyPasteCode,
    string? QrCodeImage,
    string? RedirectUrl,
    string? Message,
    DateTimeOffset? ExpiresAt);

[ApiController]
[Route("Paywall")]
[Produces("application/json")]
public sealed class PaywallController(
    GetAccessStatus accessStatus,
    StartCheckout startCheckout,
    IUserManager userManager,
    ILogger<PaywallController> logger) : ControllerBase
{
    [HttpGet("Status")]
    [Authorize]
    public async Task<ActionResult<AccessStatusView>> GetStatus(CancellationToken cancellationToken)
    {
        if (CurrentUser.IdOf(User) is not { } userId)
        {
            return Unauthorized();
        }

        return await accessStatus.ExecuteAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    [HttpPost("Checkout")]
    [Authorize]
    public async Task<ActionResult<CheckoutView>> Checkout(
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

        var command = new StartCheckoutCommand(userId, user.Username, body.PlanId, body.ProviderKey);

        try
        {
            var ticket = await startCheckout.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);

            return Describe(ticket);
        }
        catch (PaywallException failure)
        {
            logger.LogWarning(failure, "Paywall: checkout de {User} não pôde ser aberto.", user.Username);

            return BadRequest(new { error = failure.UserMessage });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception failure)
        {
            logger.LogError(failure, "Paywall: provedor {Provider} falhou no checkout.", body.ProviderKey);

            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { error = "O meio de pagamento não respondeu. Tente de novo em instantes." });
        }
    }

    private static CheckoutView Describe(CheckoutTicket ticket) => new(
        ticket.Instructions.CopyPasteCode,
        ticket.Instructions.QrCodeImage,
        ticket.Instructions.RedirectUrl?.ToString(),
        ticket.Instructions.Message,
        ticket.Instructions.ExpiresAt);
}
