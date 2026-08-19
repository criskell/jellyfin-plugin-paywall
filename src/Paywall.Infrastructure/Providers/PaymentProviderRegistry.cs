using Paywall.Application;
using Paywall.Application.Payments;

namespace Paywall.Infrastructure.Providers;

public sealed class PaymentProviderRegistry(IEnumerable<IPaymentProvider> providers) : IPaymentProviderRegistry
{
    public IReadOnlyCollection<IPaymentProvider> Available =>
        providers.Where(provider => provider.IsConfigured).ToArray();

    public IPaymentProvider Resolve(string key)
    {
        var match = providers.FirstOrDefault(provider =>
            string.Equals(provider.Key, key, StringComparison.OrdinalIgnoreCase) && provider.IsConfigured);

        return match ?? throw new PaymentProviderNotFoundException(key);
    }
}
