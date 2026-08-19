using Microsoft.Data.Sqlite;
using Paywall.Application.Ports;
using Paywall.Domain;

namespace Paywall.Infrastructure.Storage;

public sealed class SqliteAccessGrantRepository(PaywallDatabase database) : IAccessGrantRepository
{
    private const string Columns =
        "user_id, plan_id, expires_at, subscription_provider, subscription_reference, revoked, "
        + "price_cents, currency, duration_days";

    public async Task<AccessGrant?> FindAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();

        command.CommandText = $"SELECT {Columns} FROM access_grants WHERE user_id = $user";
        command.Bind("$user", userId.ToText());

        return await ReadOneAsync(command, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AccessGrant?> FindBySubscriptionAsync(
        Subscription subscription,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();

        command.CommandText = $"""
            SELECT {Columns} FROM access_grants
            WHERE subscription_provider = $provider AND subscription_reference = $reference
            """;
        command.Bind("$provider", subscription.ProviderKey);
        command.Bind("$reference", subscription.Reference);

        return await ReadOneAsync(command, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveAsync(AccessGrant grant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grant);

        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();

        command.CommandText = """
            INSERT INTO access_grants
                (user_id, plan_id, expires_at, subscription_provider, subscription_reference, revoked,
                 price_cents, currency, duration_days)
            VALUES ($user, $plan, $expires, $provider, $reference, $revoked,
                    $price, $currency, $duration)
            ON CONFLICT (user_id) DO UPDATE SET
                plan_id = excluded.plan_id,
                expires_at = excluded.expires_at,
                subscription_provider = excluded.subscription_provider,
                subscription_reference = excluded.subscription_reference,
                revoked = excluded.revoked,
                price_cents = excluded.price_cents,
                currency = excluded.currency,
                duration_days = excluded.duration_days
            """;

        command.Bind("$user", grant.UserId.ToText());
        command.Bind("$plan", grant.PlanId);
        command.Bind("$expires", grant.ExpiresAt?.ToText());
        command.Bind("$provider", grant.Subscription?.ProviderKey);
        command.Bind("$reference", grant.Subscription?.Reference);
        command.Bind("$revoked", grant.Revoked ? 1 : 0);
        command.Bind("$price", grant.Terms?.Price.Cents ?? 0);
        command.Bind("$currency", grant.Terms?.Price.Currency ?? "BRL");
        command.Bind("$duration", grant.Terms?.Duration.Days ?? 0);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<AccessGrant?> ReadOneAsync(SqliteCommand command, CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var providerKey = reader.ReadOptionalText(3);
        var reference = reader.ReadOptionalText(4);

        return AccessGrant.Restore(
            reader.ReadGuid(0),
            ReadTerms(reader),
            reader.ReadOptionalInstant(2),
            providerKey is not null && reference is not null ? new Subscription(providerKey, reference) : null,
            reader.GetInt32(5) != 0);
    }

    private static PlanTerms? ReadTerms(SqliteDataReader reader)
    {
        if (reader.ReadOptionalText(1) is not { } planId)
        {
            return null;
        }

        var days = reader.GetInt32(8);

        return PlanTerms.Restore(
            planId,
            Money.Of(reader.GetInt64(6), reader.GetString(7)),
            days > 0 ? AccessDuration.OfDays(days) : AccessDuration.Lifetime);
    }
}
