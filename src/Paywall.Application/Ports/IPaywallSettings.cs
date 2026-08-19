namespace Paywall.Application.Ports;

public interface IPaywallSettings
{
    TimeSpan GracePeriod { get; }
}
