namespace Paywall.Application.Ports;

public sealed record Subscriber(Guid UserId, string Name);

/// <summary>
/// Usuários sujeitos ao paywall. Administradores e isentos ficam de fora na implementação.
/// </summary>
public interface ISubscriberDirectory
{
    Task<IReadOnlyCollection<Subscriber>> ListAsync(CancellationToken cancellationToken);
}
