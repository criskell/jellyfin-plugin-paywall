namespace Paywall.Application.Ports;

public enum AccessDecision
{
    Allow,
    Deny
}

public interface IAccessEnforcer
{
    Task ApplyAsync(Guid userId, AccessDecision decision, CancellationToken cancellationToken);
}
