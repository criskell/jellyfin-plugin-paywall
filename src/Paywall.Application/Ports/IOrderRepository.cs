using Paywall.Domain;

namespace Paywall.Application.Ports;

public interface IOrderRepository
{
    Task<Order?> FindAsync(Guid orderId, CancellationToken cancellationToken);

    Task<Order?> FindByReferenceAsync(string providerKey, string providerReference, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Order>> ListByUserAsync(Guid userId, CancellationToken cancellationToken);

    Task SaveAsync(Order order, CancellationToken cancellationToken);
}
