namespace Paywall.Application.Ports;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IIdentifierFactory
{
    Guid NewId();
}
