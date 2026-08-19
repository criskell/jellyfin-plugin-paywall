using Paywall.Application.Ports;
using Paywall.Domain;

namespace Paywall.Application.UseCases;

public sealed record GrantAccessManuallyCommand(Guid UserId, string PlanId);

/// <summary>
/// Libera acesso sem cobrança: cortesia, teste, ou Pix recebido fora do sistema e conferido
/// pelo administrador no extrato.
/// </summary>
public sealed class GrantAccessManually(
    IPlanCatalog plans,
    IAccessGrantRepository grants,
    IAccessEnforcer enforcer,
    IClock clock)
{
    public async Task<AccessGrant> ExecuteAsync(GrantAccessManuallyCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var plan = plans.Find(command.PlanId) ?? throw new PlanNotFoundException(command.PlanId);
        var grant = await grants.FindAsync(command.UserId, cancellationToken).ConfigureAwait(false)
                    ?? AccessGrant.NeverPaid(command.UserId);

        grant.Extend(plan, clock.UtcNow);

        await grants.SaveAsync(grant, cancellationToken).ConfigureAwait(false);
        await enforcer.ApplyAsync(command.UserId, AccessDecision.Allow, cancellationToken).ConfigureAwait(false);

        return grant;
    }
}
