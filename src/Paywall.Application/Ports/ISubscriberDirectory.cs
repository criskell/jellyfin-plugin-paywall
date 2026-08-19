namespace Paywall.Application.Ports;

public sealed record Subscriber(Guid UserId, string Name);

public interface ISubscriberDirectory
{
    Task<IReadOnlyCollection<Subscriber>> ListAsync(CancellationToken cancellationToken);

    Task<bool> IsSubjectAsync(Guid userId, CancellationToken cancellationToken);
}
