using Paywall.Application.Ports;
using Paywall.Domain;

namespace Paywall.Infrastructure.Storage;

public sealed class SqliteAccessGrantRepository(PaywallDatabase database) : IAccessGrantRepository
{
    public async Task<AccessGrant?> FindAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT user_id, plan_id, expires_at, subscription_reference
            FROM access_grants WHERE user_id = $user
            """;
        command.Bind("$user", userId.ToText());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return AccessGrant.Restore(
            reader.ReadGuid(0),
            reader.ReadOptionalText(1),
            reader.ReadOptionalInstant(2),
            reader.ReadOptionalText(3));
    }

    public async Task SaveAsync(AccessGrant grant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grant);

        await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();

        command.CommandText = """
            INSERT INTO access_grants (user_id, plan_id, expires_at, subscription_reference)
            VALUES ($user, $plan, $expires, $subscription)
            ON CONFLICT (user_id) DO UPDATE SET
                plan_id = excluded.plan_id,
                expires_at = excluded.expires_at,
                subscription_reference = excluded.subscription_reference
            """;

        command.Bind("$user", grant.UserId.ToText());
        command.Bind("$plan", grant.PlanId);
        command.Bind("$expires", grant.ExpiresAt?.ToText());
        command.Bind("$subscription", grant.SubscriptionReference);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
