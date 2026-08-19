using Microsoft.Data.Sqlite;
using Paywall.Application.Ports;
using Paywall.Domain;

namespace Paywall.Infrastructure.Storage;

public sealed class SqliteOrderRepository(PaywallDatabase database) : IOrderRepository
{
    private const string Columns =
        "id, user_id, plan_id, provider_key, provider_reference, amount_cents, currency, status, created_at, settled_at";

    public async Task<Order?> FindAsync(Guid orderId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();

        command.CommandText = $"SELECT {Columns} FROM orders WHERE id = $id";
        command.Bind("$id", orderId.ToText());

        return await ReadOneAsync(command, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Order?> FindByReferenceAsync(
        string providerKey,
        string providerReference,
        CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();

        command.CommandText = $"""
            SELECT {Columns} FROM orders
            WHERE provider_key = $provider AND provider_reference = $reference
            ORDER BY created_at DESC LIMIT 1
            """;
        command.Bind("$provider", providerKey);
        command.Bind("$reference", providerReference);

        return await ReadOneAsync(command, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveAsync(Order order, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);

        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();

        command.CommandText = """
            INSERT INTO orders (id, user_id, plan_id, provider_key, provider_reference,
                                amount_cents, currency, status, created_at, settled_at)
            VALUES ($id, $user, $plan, $provider, $reference,
                    $amount, $currency, $status, $created, $settled)
            ON CONFLICT (id) DO UPDATE SET
                provider_reference = excluded.provider_reference,
                status = excluded.status,
                settled_at = excluded.settled_at
            """;

        command.Bind("$id", order.Id.ToText());
        command.Bind("$user", order.UserId.ToText());
        command.Bind("$plan", order.PlanId);
        command.Bind("$provider", order.ProviderKey);
        command.Bind("$reference", order.ProviderReference);
        command.Bind("$amount", order.Amount.Cents);
        command.Bind("$currency", order.Amount.Currency);
        command.Bind("$status", (int)order.Status);
        command.Bind("$created", order.CreatedAt.ToText());
        command.Bind("$settled", order.SettledAt?.ToText());

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Order?> ReadOneAsync(SqliteCommand command, CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? Map(reader) : null;
    }

    private static Order Map(SqliteDataReader reader) => Order.Restore(
        reader.ReadGuid(0),
        reader.ReadGuid(1),
        reader.GetString(2),
        reader.GetString(3),
        Money.Of(reader.GetInt64(5), reader.GetString(6)),
        (OrderStatus)reader.GetInt32(7),
        reader.ReadInstant(8),
        reader.ReadOptionalInstant(9),
        reader.ReadOptionalText(4));
}
