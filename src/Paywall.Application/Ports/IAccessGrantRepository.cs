using Paywall.Domain;

namespace Paywall.Application.Ports;

public interface IAccessGrantRepository
{
    Task<AccessGrant?> FindAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Acha o assinante pela recorrência. É como uma renovação, que chega sem pedido aberto,
    /// descobre de quem é.
    /// </summary>
    Task<AccessGrant?> FindBySubscriptionAsync(string subscriptionReference, CancellationToken cancellationToken);

    Task SaveAsync(AccessGrant grant, CancellationToken cancellationToken);
}
