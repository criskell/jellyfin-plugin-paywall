namespace Paywall.Application.Payments;

public sealed record PaymentInstructions
{
    public string? CopyPasteCode { get; init; }

    public string? QrCodeImage { get; init; }

    public Uri? RedirectUrl { get; init; }

    public string? Message { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }
}
