using Paywall.Domain;

namespace Paywall.Application.Ports;

public interface IAccessGrantRepository
{
    Task<AccessGrant?> FindAsync(Guid userId, CancellationToken cancellationToken);

    Task<AccessGrant?> FindBySubscriptionAsync(Subscription subscription, CancellationToken cancellationToken);

    Task SaveAsync(AccessGrant grant, CancellationToken cancellationToken);
}
