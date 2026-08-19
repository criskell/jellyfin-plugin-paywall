using Paywall.Application.Ports;
using Paywall.Application.UseCases;
using Paywall.Domain;
using Xunit;

namespace Paywall.Tests;

public class AccessLifecycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Subscriber = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static Plan Monthly =>
        new("mensal", "Mensal", Money.Of(1990), BillingMode.Recurring, AccessDuration.OfDays(30));

    [Fact]
    public async Task ContaNovaEBloqueadaAntesDaPrimeiraVarredura()
    {
        var enforcer = new RecordingEnforcer();

        await Apply(Subscriber, new InMemoryGrants(), enforcer);

        Assert.Equal([(Subscriber, AccessDecision.Deny)], enforcer.Applied);
    }

    [Fact]
    public async Task UsuarioIsentoNaoERegulado()
    {
        var enforcer = new RecordingEnforcer();
        var useCase = new ApplyCurrentAccess(
            new FakeDirectory(),
            new InMemoryGrants(),
            enforcer,
            new FixedSettings(TimeSpan.Zero),
            new FixedClock(Now));

        var decision = await useCase.ExecuteAsync(Subscriber, CancellationToken.None);

        Assert.Null(decision);
        Assert.Empty(enforcer.Applied);
    }

    [Fact]
    public async Task PlanoVencidoEntreVarredurasCaiNoLogin()
    {
        var grants = new InMemoryGrants();
        var grant = AccessGrant.NeverPaid(Subscriber);
        grant.Extend(Monthly, Now.AddDays(-40));
        await grants.SaveAsync(grant, CancellationToken.None);
        var enforcer = new RecordingEnforcer();

        await Apply(Subscriber, grants, enforcer);

        Assert.Equal([(Subscriber, AccessDecision.Deny)], enforcer.Applied);
    }

    [Fact]
    public async Task RevogarCancelaSomenteNoProvedorQueEmitiuARecorrencia()
    {
        var asaas = new CancellableProvider("asaas");
        var outro = new CancellableProvider("outro");
        var grants = new InMemoryGrants();
        var grant = AccessGrant.NeverPaid(Subscriber);
        grant.Extend(Monthly, Now);
        grant.AttachSubscription(new Subscription("asaas", "sub_42"));
        await grants.SaveAsync(grant, CancellationToken.None);

        var revoke = new RevokeAccess(
            grants,
            new RecordingEnforcer(),
            new FakeRegistry(asaas, outro),
            new FixedClock(Now));
        await revoke.ExecuteAsync(Subscriber, CancellationToken.None);

        Assert.Equal(["sub_42"], asaas.Canceled);
        Assert.Empty(outro.Canceled);
    }

    [Fact]
    public async Task RevogarFuncionaMesmoComOProvedorDesconfigurado()
    {
        var grants = new InMemoryGrants();
        var grant = AccessGrant.NeverPaid(Subscriber);
        grant.Extend(Monthly, Now);
        grant.AttachSubscription(new Subscription("provedor-removido", "sub_42"));
        await grants.SaveAsync(grant, CancellationToken.None);
        var enforcer = new RecordingEnforcer();

        var revoke = new RevokeAccess(grants, enforcer, new FakeRegistry(), new FixedClock(Now));
        await revoke.ExecuteAsync(Subscriber, CancellationToken.None);

        Assert.Contains((Subscriber, AccessDecision.Deny), enforcer.Applied);
        var revoked = await grants.FindAsync(Subscriber, CancellationToken.None);
        Assert.False(revoked!.IsActiveAt(Now.AddSeconds(1), TimeSpan.Zero));
    }

    private static Task<AccessDecision?> Apply(Guid userId, InMemoryGrants grants, RecordingEnforcer enforcer)
    {
        var useCase = new ApplyCurrentAccess(
            new FakeDirectory(userId),
            grants,
            enforcer,
            new FixedSettings(TimeSpan.Zero),
            new FixedClock(Now));

        return useCase.ExecuteAsync(userId, CancellationToken.None);
    }
}
