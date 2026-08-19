namespace Paywall.Application.Payments;

/// <summary>
/// Cobrança aberta no provedor. A referência é o que amarra o webhook de volta ao pedido.
/// </summary>
public sealed record CheckoutTicket(string ProviderReference, PaymentInstructions Instructions);
