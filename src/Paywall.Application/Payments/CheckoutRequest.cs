using Paywall.Domain;

namespace Paywall.Application.Payments;

/// <summary>Dados que qualquer provedor precisa para abrir uma cobrança.</summary>
public sealed record CheckoutRequest(Guid OrderId, Plan Plan, Payer Payer, Uri WebhookUrl)
{
    /// <summary>Para onde devolver o usuário depois de um checkout hospedado, se houver.</summary>
    public Uri? ReturnUrl { get; init; }
}

/// <summary>Quem paga. CPF só é preenchido quando o provedor exige, como no Pix Automático.</summary>
public sealed record Payer(Guid UserId, string Name)
{
    public string? Email { get; init; }

    public string? TaxId { get; init; }
}
