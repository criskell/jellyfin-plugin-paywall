namespace Paywall.Application;

public class PaywallException : Exception
{
    public PaywallException(string message) : base(message)
    {
    }
}

public sealed class PlanNotFoundException(string planId)
    : PaywallException($"Plano '{planId}' não existe.");

public sealed class PaymentProviderNotFoundException(string providerKey)
    : PaywallException($"Método de pagamento '{providerKey}' não está disponível.");

public sealed class UnsupportedBillingModeException(string providerKey, string mode)
    : PaywallException($"Método de pagamento '{providerKey}' não atende cobrança do tipo {mode}.");

public sealed class MissingPublicUrlException()
    : PaywallException("Configure o endereço público do servidor para receber webhooks.");
