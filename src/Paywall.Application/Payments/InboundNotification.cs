namespace Paywall.Application.Payments;

public sealed record InboundNotification(
    string ProviderKey,
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyDictionary<string, string> Query,
    string Body);
