using Microsoft.Data.Sqlite;

namespace Paywall.Infrastructure.Storage;

/// <summary>
/// Banco próprio do plugin, num arquivo separado do <c>jellyfin.db</c>. O servidor é dono do
/// schema dele e o migra a cada release; misturar tabelas ali deixaria os dados de cobrança
/// reféns de migrations que não são nossas.
/// </summary>
public sealed class PaywallDatabase
{
    private const int CurrentSchemaVersion = 1;

    private readonly string _connectionString;
    private readonly SemaphoreSlim _bootstrapGate = new(1, 1);
    private bool _ready;

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
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        return connection;
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (_ready)
        {
            return;
        }

        await _bootstrapGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_ready)
            {
                return;
            }

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ExecuteAsync(connection, SchemaScript, cancellationToken).ConfigureAwait(false);

            _ready = true;
        }
        finally
        {
            _bootstrapGate.Release();
        }
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// WAL deixa a varredura de vencimentos ler enquanto um webhook grava.
    /// </summary>
    private static string SchemaScript => $"""
        PRAGMA journal_mode = WAL;
        PRAGMA foreign_keys = ON;

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

        CREATE INDEX IF NOT EXISTS ix_grants_subscription
            ON access_grants (subscription_reference)
            WHERE subscription_reference IS NOT NULL;

        PRAGMA user_version = {CurrentSchemaVersion};
        """;
}
