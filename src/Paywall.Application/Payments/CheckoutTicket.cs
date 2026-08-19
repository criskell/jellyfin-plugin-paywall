namespace Paywall.Application.Payments;

public sealed record CheckoutTicket(string ProviderReference, PaymentInstructions Instructions);
