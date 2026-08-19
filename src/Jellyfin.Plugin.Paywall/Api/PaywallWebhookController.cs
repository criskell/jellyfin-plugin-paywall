using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Paywall.Application;
using Paywall.Infrastructure;
using Paywall.Application.Payments;
using Paywall.Application.UseCases;

namespace Jellyfin.Plugin.Paywall.Api;

[ApiController]
[Route("Paywall/Webhook")]
public sealed class PaywallWebhookController(
    ConfirmPayment confirmPayment,
    PaymentNotificationGate gate,
    ILogger<PaywallWebhookController> logger) : ControllerBase
{
    [HttpPost("{providerKey}")]
    [AllowAnonymous]
    public async Task<ActionResult> Receive(string providerKey, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

        var notification = new InboundNotification(
            providerKey,
            Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase),
            Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString(), StringComparer.OrdinalIgnoreCase),
            body);

        try
        {
            var outcome = await gate
                .EnterAsync(() => confirmPayment.ExecuteAsync(notification, cancellationToken), cancellationToken)
                .ConfigureAwait(false);
            logger.LogInformation("Paywall: webhook de {Provider} resultou em {Outcome}.", providerKey, outcome);

            return outcome == ConfirmPaymentOutcome.OrderNotFound ? NotFound() : NoContent();
        }
        catch (PaywallException failure)
        {
            logger.LogWarning("Paywall: webhook de {Provider} recusado. {Reason}", providerKey, failure.Message);
            return BadRequest();
        }
    }
}
