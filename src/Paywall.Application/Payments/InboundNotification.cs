namespace Paywall.Application.Payments;

/// <summary>
/// Webhook recebido, já traduzido para tipos simples. Nenhum tipo de HTTP atravessa a fronteira.
/// </summary>
public sealed record InboundNotification(
    string ProviderKey,
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyDictionary<string, string> Query,
    string Body);
