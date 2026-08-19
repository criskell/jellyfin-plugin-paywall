using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Paywall.Infrastructure.Storage;

public sealed class PaywallDatabase
{
    private const string EnableConcurrentReads = "PRAGMA journal_mode = WAL;";

    private static readonly string[] SchemaVersions =
    [
        """
        CREATE TABLE IF NOT EXISTS orders (
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

        CREATE INDEX IF NOT EXISTS ix_orders_reference ON orders (provider_key, provider_reference);
        CREATE INDEX IF NOT EXISTS ix_orders_user ON orders (user_id, created_at DESC);

        CREATE TABLE IF NOT EXISTS access_grants (
            user_id                 TEXT NOT NULL PRIMARY KEY,
            plan_id                 TEXT NULL,
            expires_at              TEXT NULL,
            subscription_reference  TEXT NULL
        );
        """,

        """
        ALTER TABLE access_grants ADD COLUMN subscription_provider TEXT NULL;

        CREATE INDEX IF NOT EXISTS ix_grants_subscription
            ON access_grants (subscription_provider, subscription_reference)
            WHERE subscription_reference IS NOT NULL;
        """
    ];

    private readonly string _connectionString;
    private readonly SemaphoreSlim _migrationGate = new(1, 1);
    private bool _migrated;

    public PaywallDatabase(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(dataDirectory, "paywall.db"),
            Pooling = true
        }.ToString();
    }

    public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        await MigrateAsync(cancellationToken).ConfigureAwait(false);

        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        return connection;
    }

    private async Task MigrateAsync(CancellationToken cancellationToken)
    {
        if (_migrated)
        {
            return;
        }

        await _migrationGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_migrated)
            {
                return;
            }

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await ExecuteAsync(connection, EnableConcurrentReads, cancellationToken).ConfigureAwait(false);

            var applied = await ReadVersionAsync(connection, cancellationToken).ConfigureAwait(false);

            for (var version = applied; version < SchemaVersions.Length; version++)
            {
                await ExecuteAsync(connection, SchemaVersions[version], cancellationToken).ConfigureAwait(false);

                var next = (version + 1).ToString(CultureInfo.InvariantCulture);
                await ExecuteAsync(connection, $"PRAGMA user_version = {next};", cancellationToken)
                    .ConfigureAwait(false);
            }

            _migrated = true;
        }
        finally
        {
            _migrationGate.Release();
        }
    }

    private static async Task<int> ReadVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";

        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

        return value is null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
