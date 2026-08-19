namespace Paywall.Application.Ports;

/// <summary>
/// Resolve a URL pública que o provedor deve chamar. A rota é detalhe da camada de entrega.
/// </summary>
public interface IWebhookEndpoints
{
    Uri For(string providerKey);
}
