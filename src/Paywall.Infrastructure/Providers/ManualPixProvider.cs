using Paywall.Application.Payments;
using Paywall.Domain;

namespace Paywall.Infrastructure.Providers;

public interface IManualPixOptions
{
    string? PixKey { get; }

    string? PayeeName { get; }

    string? PayeeCity { get; }

    string? Instructions { get; }
}

/// <summary>
/// Pix direto na sua chave, sem intermediário. Gera o copia e cola com valor certo, mas
/// não tem como saber que o dinheiro entrou: a liberação é feita pelo administrador.
/// </summary>
public sealed class ManualPixProvider(IManualPixOptions options) : IPaymentProvider
{
    public string Key => "manual-pix";

    public string DisplayName => "Pix (confirmação manual)";

    public IReadOnlyCollection<BillingMode> SupportedModes { get; } = [BillingMode.OneTime];

    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.PixKey);

    public Task<CheckoutTicket> StartCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reference = request.OrderId.ToString("N");

        var brCode = PixBrCode.Build(new PixBrCodeRequest(options.PixKey!, request.Plan.Price.Amount, reference)
        {
            PayeeName = options.PayeeName,
            PayeeCity = options.PayeeCity
        });

        var instructions = new PaymentInstructions
        {
            CopyPasteCode = brCode,
            Message = options.Instructions
                      ?? "Pague o Pix e envie o comprovante. A liberação é feita manualmente."
        };

        return Task.FromResult(new CheckoutTicket(reference, instructions));
    }

    /// <summary>Sem PSP não existe webhook: nada a interpretar.</summary>
    public Task<PaymentEvent?> InterpretAsync(InboundNotification notification, CancellationToken cancellationToken) =>
        Task.FromResult<PaymentEvent?>(null);
}
