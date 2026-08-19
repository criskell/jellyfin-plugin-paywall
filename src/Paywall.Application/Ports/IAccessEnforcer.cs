namespace Paywall.Application.Ports;

public enum AccessDecision
{
    Allow,
    Deny
}

/// <summary>
/// Quem efetivamente abre ou fecha a porta no servidor de mídia. Implementado fora do núcleo.
/// </summary>
public interface IAccessEnforcer
{
    Task ApplyAsync(Guid userId, AccessDecision decision, CancellationToken cancellationToken);
}
