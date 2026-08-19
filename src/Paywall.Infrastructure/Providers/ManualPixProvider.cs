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

    public Task<PaymentEvent?> ReadPaymentEventAsync(InboundNotification notification, CancellationToken cancellationToken) =>
        Task.FromResult<PaymentEvent?>(null);
}
