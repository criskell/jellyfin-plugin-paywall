using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Paywall.Application;
using Paywall.Application.Payments;
using Paywall.Application.UseCases;

namespace Jellyfin.Plugin.Paywall.Api;

/// <summary>
/// Porta de entrada dos provedores. Anônima por necessidade: quem valida a autenticidade
/// da notificação é o adaptador do provedor, que conhece a assinatura dele.
/// </summary>
[ApiController]
[Route("Paywall/Webhook")]
public sealed class PaywallWebhookController(
    ConfirmPayment confirmPayment,
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
            var outcome = await confirmPayment.ExecuteAsync(notification, cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Paywall: webhook de {Provider} resultou em {Outcome}.", providerKey, outcome);

            // Erro conhecido não pode virar 5xx: o provedor reenviaria o mesmo evento sem parar.
            return outcome == ConfirmPaymentOutcome.OrderNotFound ? NotFound() : Ok(new { outcome = outcome.ToString() });
        }
        catch (PaywallException failure)
        {
            logger.LogWarning("Paywall: webhook de {Provider} recusado. {Reason}", providerKey, failure.Message);
            return BadRequest();
        }
    }
}
