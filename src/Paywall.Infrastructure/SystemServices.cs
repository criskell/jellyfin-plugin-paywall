using Paywall.Application.Ports;

namespace Paywall.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class GuidIdentifierFactory : IIdentifierFactory
{
    public Guid NewId() => Guid.NewGuid();
}
