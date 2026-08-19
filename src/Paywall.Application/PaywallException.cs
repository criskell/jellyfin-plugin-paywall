namespace Paywall.Application;

public class PaywallException(string message) : Exception(message)
{
    private const string Generic = "Não foi possível concluir a operação. Tente de novo ou fale com o administrador.";

    public virtual string UserMessage => Generic;
}

public sealed class PlanNotFoundException(string planId)
    : PaywallException($"Plano '{planId}' não existe.")
{
    public override string UserMessage => "Esse plano não está disponível.";
}

public sealed class PaymentProviderNotFoundException(string providerKey)
    : PaywallException($"Método de pagamento '{providerKey}' não está disponível.")
{
    public override string UserMessage => "Esse meio de pagamento não está disponível.";
}

public sealed class UnsupportedBillingModeException(string providerKey, string mode)
    : PaywallException($"Método de pagamento '{providerKey}' não atende cobrança do tipo {mode}.")
{
    public override string UserMessage => "Esse meio de pagamento não atende esse tipo de plano.";
}

public sealed class MissingPublicUrlException()
    : PaywallException("Configure o endereço público do servidor para receber webhooks.");
