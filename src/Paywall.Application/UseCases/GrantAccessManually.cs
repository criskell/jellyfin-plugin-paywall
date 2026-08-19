using Paywall.Application.Ports;
using Paywall.Domain;

namespace Paywall.Application.UseCases;

public sealed record GrantAccessManuallyCommand(Guid UserId, string PlanId);

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

        grant.Extend(PlanTerms.Of(plan), clock.UtcNow);

        await grants.SaveAsync(grant, cancellationToken).ConfigureAwait(false);
        await enforcer.ApplyAsync(command.UserId, AccessDecision.Allow, cancellationToken).ConfigureAwait(false);

        return grant;
    }
}
