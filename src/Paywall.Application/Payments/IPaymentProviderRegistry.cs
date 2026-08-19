namespace Paywall.Application.Payments;

public interface IPaymentProviderRegistry
{
    /// <summary>Provedores com credenciais completas, na ordem em que devem aparecer ao usuário.</summary>
    IReadOnlyCollection<IPaymentProvider> Available { get; }

    IPaymentProvider Resolve(string key);
}
