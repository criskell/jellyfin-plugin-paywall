namespace Paywall.Application.Payments;

/// <summary>
/// O que mostrar ao usuário para ele pagar. Cada provedor preenche o que sabe produzir:
/// Pix devolve copia-e-cola e QR, redirecionamentos devolvem uma URL.
/// </summary>
public sealed record PaymentInstructions
{
    public string? CopyPasteCode { get; init; }

    /// <summary>Imagem do QR Code como data URI, quando o provedor a fornece pronta.</summary>
    public string? QrCodeImage { get; init; }

    public Uri? RedirectUrl { get; init; }

    public string? Message { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }
}
