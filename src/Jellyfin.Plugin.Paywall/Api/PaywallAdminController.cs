using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Paywall.Application;
using Paywall.Application.UseCases;

namespace Jellyfin.Plugin.Paywall.Api;

public sealed record GrantAccessBody(string PlanId);

[ApiController]
[Route("Paywall/Admin")]
[Authorize(Policy = "RequiresElevation")]
[Produces("application/json")]
public sealed class PaywallAdminController(
    GrantAccessManually grantAccess,
    RevokeAccess revokeAccess,
    SyncAccess syncAccess) : ControllerBase
{
    /// <summary>Libera acesso sem cobrança, para Pix conferido no extrato ou cortesia.</summary>
    [HttpPost("Users/{userId:guid}/Grant")]
    public async Task<ActionResult> Grant(
        Guid userId,
        [FromBody] GrantAccessBody body,
        CancellationToken cancellationToken)
    {
        try
        {
            var grant = await grantAccess
                .ExecuteAsync(new GrantAccessManuallyCommand(userId, body.PlanId), cancellationToken)
                .ConfigureAwait(false);

            return Ok(new { grant.PlanId, grant.ExpiresAt });
        }
        catch (PaywallException failure)
        {
            return BadRequest(new { error = failure.Message });
        }
    }

    [HttpPost("Users/{userId:guid}/Revoke")]
    public async Task<ActionResult> Revoke(Guid userId, CancellationToken cancellationToken)
    {
        await revokeAccess.ExecuteAsync(userId, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>Reaplica as decisões agora, sem esperar a tarefa agendada.</summary>
    [HttpPost("Sync")]
    public async Task<ActionResult<AccessSyncReport>> Sync(CancellationToken cancellationToken) =>
        await syncAccess.ExecuteAsync(cancellationToken).ConfigureAwait(false);
}
