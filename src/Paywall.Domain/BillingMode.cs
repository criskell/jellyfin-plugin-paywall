namespace Paywall.Domain;

/// <summary>
/// Como o plano é cobrado. Determina quais provedores podem atendê-lo.
/// </summary>
public enum BillingMode
{
    /// <summary>Pagamento único que libera acesso por um prazo fixo ou vitalício.</summary>
    OneTime,

    /// <summary>Assinatura renovada a cada período pelo provedor.</summary>
    Recurring
}
