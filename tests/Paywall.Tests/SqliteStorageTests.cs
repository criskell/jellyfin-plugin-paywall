using Microsoft.Data.Sqlite;
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
        Assert.Equal(1990, loaded.Terms.Price.Cents);
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

        grant.Extend(PlanTerms.Of(plan), Now);
        await repository.SaveAsync(grant, CancellationToken.None);
        grant.Extend(PlanTerms.Of(plan), Now.AddDays(10));
        await repository.SaveAsync(grant, CancellationToken.None);

        var loaded = await repository.FindAsync(grant.UserId, CancellationToken.None);

        Assert.Equal(Now.AddDays(60), loaded?.ExpiresAt);
    }

    [Fact]
    public async Task RecorrenciaEncontraOAssinantePeloProvedorEReferencia()
    {
        var repository = new SqliteAccessGrantRepository(Database);
        var plan = new Plan("mensal", "Mensal", Money.Of(1990), BillingMode.Recurring, AccessDuration.OfDays(30));
        var grant = AccessGrant.NeverPaid(Guid.NewGuid());
        grant.Extend(PlanTerms.Of(plan), Now);
        grant.AttachSubscription(new Subscription("asaas", "sub_42"));
        await repository.SaveAsync(grant, CancellationToken.None);

        var found = await repository.FindBySubscriptionAsync(
            new Subscription("asaas", "sub_42"),
            CancellationToken.None);

        Assert.Equal(grant.UserId, found?.UserId);
        Assert.Null(await repository.FindBySubscriptionAsync(
            new Subscription("outro", "sub_42"),
            CancellationToken.None));
    }

    [Fact]
    public async Task BancoNaVersaoAnteriorEMigradoSemPerderDados()
    {
        Directory.CreateDirectory(_directory);
        var userId = Guid.NewGuid();

        await using (var connection = new SqliteConnection("Data Source=" + Path.Combine(_directory, "paywall.db")))
        {
            await connection.OpenAsync(CancellationToken.None);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE orders (
                    id                  TEXT    NOT NULL PRIMARY KEY,
                    user_id             TEXT    NOT NULL,
                    plan_id             TEXT    NOT NULL,
                    provider_key        TEXT    NOT NULL,
                    provider_reference  TEXT    NULL,
                    amount_cents        INTEGER NOT NULL,
                    currency            TEXT    NOT NULL,
                    status              INTEGER NOT NULL,
                    created_at          TEXT    NOT NULL,
                    settled_at          TEXT    NULL
                );

                CREATE TABLE access_grants (
                    user_id                 TEXT NOT NULL PRIMARY KEY,
                    plan_id                 TEXT NULL,
                    expires_at              TEXT NULL,
                    subscription_reference  TEXT NULL
                );

                INSERT INTO access_grants (user_id, plan_id, expires_at)
                VALUES ('$USER', 'mensal', '2026-09-17T12:00:00.0000000+00:00');

                PRAGMA user_version = 1;
                """.Replace("$USER", userId.ToString("N"));
            await command.ExecuteNonQueryAsync(CancellationToken.None);
        }

        var repository = new SqliteAccessGrantRepository(Database);
        var migrated = await repository.FindAsync(userId, CancellationToken.None);

        Assert.Equal("mensal", migrated?.PlanId);
        Assert.Null(migrated?.Subscription);
        Assert.False(migrated?.Revoked);
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
