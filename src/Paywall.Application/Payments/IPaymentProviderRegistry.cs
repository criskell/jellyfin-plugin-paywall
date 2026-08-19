namespace Paywall.Application.Payments;

public interface IPaymentProviderRegistry
{
    IReadOnlyCollection<IPaymentProvider> Available { get; }

    IPaymentProvider Resolve(string key);
}
