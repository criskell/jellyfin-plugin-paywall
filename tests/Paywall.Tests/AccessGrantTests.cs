using Paywall.Domain;
using Xunit;

namespace Paywall.Tests;

public class AccessGrantTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);

    private static Plan Monthly => new("mensal", "Mensal", Money.Of(1990), BillingMode.Recurring, AccessDuration.OfDays(30));

    [Fact]
    public void UsuarioSemPagamentoNaoTemAcesso()
    {
        var grant = AccessGrant.NeverPaid(Guid.NewGuid());

        Assert.False(grant.IsActiveAt(Now, TimeSpan.Zero));
    }

    [Fact]
    public void PagamentoLiberaAcessoPeloPrazoDoPlano()
    {
        var grant = AccessGrant.NeverPaid(Guid.NewGuid());

        grant.Extend(Monthly, Now);

        Assert.Equal(Now.AddDays(30), grant.ExpiresAt);
    }

    [Fact]
    public void RenovacaoAntecipadaSomaAoPrazoRestante()
    {
        var grant = AccessGrant.NeverPaid(Guid.NewGuid());
        grant.Extend(Monthly, Now);

        grant.Extend(Monthly, Now.AddDays(20));

        Assert.Equal(Now.AddDays(60), grant.ExpiresAt);
    }

    [Fact]
    public void PlanoVitalicioNaoVence()
    {
        var lifetime = new Plan("vitalicio", "Vitalício", Money.Of(9900), BillingMode.OneTime, AccessDuration.Lifetime);
        var grant = AccessGrant.NeverPaid(Guid.NewGuid());

        grant.Extend(lifetime, Now);

        Assert.True(grant.IsActiveAt(Now.AddYears(50), TimeSpan.Zero));
    }

    [Fact]
    public void ToleranciaSeguraOAcessoDepoisDoVencimento()
    {
        var grant = AccessGrant.NeverPaid(Guid.NewGuid());
        grant.Extend(Monthly, Now);

        Assert.True(grant.IsActiveAt(Now.AddDays(31), TimeSpan.FromDays(3)));
    }

    [Fact]
    public void AcessoCaiQuandoAToleranciaTermina()
    {
        var grant = AccessGrant.NeverPaid(Guid.NewGuid());
        grant.Extend(Monthly, Now);

        Assert.False(grant.IsActiveAt(Now.AddDays(34), TimeSpan.FromDays(3)));
    }

    [Fact]
    public void AssinaturaExigePeriodoDeRenovacao()
    {
        Assert.Throws<ArgumentException>(() =>
            new Plan("x", "X", Money.Of(100), BillingMode.Recurring, AccessDuration.Lifetime));
    }
}
