namespace Paywall.Application.Ports;

public interface IPaywallSettings
{
    /// <summary>Tolerância depois do vencimento antes de cortar o acesso.</summary>
    TimeSpan GracePeriod { get; }
}
