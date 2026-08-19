using Paywall.Domain;
using Paywall.Infrastructure.Storage;
using Xunit;

namespace Paywall.Tests;

public class SqliteStorageTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "paywall-tests-" + Guid.NewGuid().ToString("N"));

    private PaywallDatabase Database => new(_directory);

    [Fact]
    public async Task PedidoSobreviveAoRoundTrip()
    {
        var repository = new SqliteOrderRepository(Database);
        var order = Order.Open(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new Plan("mensal", "Mensal", Money.Of(1990), BillingMode.OneTime, AccessDuration.OfDays(30)),
            "mercadopago",
            Now);
        order.TrackAs("PAY-123");

        await repository.SaveAsync(order, CancellationToken.None);
        var loaded = await repository.FindAsync(order.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(1990, loaded.Amount.Cents);
        Assert.Equal("PAY-123", loaded.ProviderReference);
        Assert.Equal(Now, loaded.CreatedAt);
    }

    [Fact]
    public async Task WebhookEncontraOPedidoPelaReferenciaDoProvedor()
    {
        var repository = new SqliteOrderRepository(Database);
        var order = Order.Open(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new Plan("mensal", "Mensal", Money.Of(1990), BillingMode.OneTime, AccessDuration.OfDays(30)),
            "asaas",
            Now);
        order.TrackAs("pay_9988");
        await repository.SaveAsync(order, CancellationToken.None);

        var found = await repository.FindByReferenceAsync("asaas", "pay_9988", CancellationToken.None);

        Assert.Equal(order.Id, found?.Id);
    }

    [Fact]
    public async Task ConcessaoRegravadaAtualizaOVencimento()
    {
        var repository = new SqliteAccessGrantRepository(Database);
        var plan = new Plan("mensal", "Mensal", Money.Of(1990), BillingMode.OneTime, AccessDuration.OfDays(30));
        var grant = AccessGrant.NeverPaid(Guid.NewGuid());

        grant.Extend(plan, Now);
        await repository.SaveAsync(grant, CancellationToken.None);
        grant.Extend(plan, Now.AddDays(10));
        await repository.SaveAsync(grant, CancellationToken.None);

        var loaded = await repository.FindAsync(grant.UserId, CancellationToken.None);

        Assert.Equal(Now.AddDays(60), loaded?.ExpiresAt);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
