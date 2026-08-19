namespace Paywall.Application.Ports;

public interface IPaywallUrls
{
    Uri Portal { get; }

    Uri WebhookFor(string providerKey);
}
