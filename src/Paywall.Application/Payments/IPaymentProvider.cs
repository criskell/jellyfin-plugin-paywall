using Paywall.Domain;

namespace Paywall.Application.Payments;

/// <summary>
/// Porta de pagamento. Trocar de PSP é escrever outra implementação desta interface,
/// sem tocar em domínio, casos de uso ou no plugin.
/// </summary>
public interface IPaymentProvider
{
    /// <summary>Chave estável usada na rota de webhook e na configuração.</summary>
    string Key { get; }

    string DisplayName { get; }

    IReadOnlyCollection<BillingMode> SupportedModes { get; }

    /// <summary>Falso enquanto faltarem credenciais, para não oferecer o método ao usuário.</summary>
    bool IsConfigured { get; }

    Task<CheckoutTicket> StartCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Valida a autenticidade da notificação e a traduz. Devolve nulo para eventos irrelevantes.
    /// </summary>
    Task<PaymentEvent?> InterpretAsync(InboundNotification notification, CancellationToken cancellationToken);
}

/// <summary>Implementado só por provedores que sabem encerrar uma recorrência.</summary>
public interface ISupportsSubscriptionCancellation
{
    Task CancelSubscriptionAsync(string subscriptionReference, CancellationToken cancellationToken);
}
