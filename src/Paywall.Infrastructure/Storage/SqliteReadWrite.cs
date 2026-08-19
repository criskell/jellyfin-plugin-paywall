using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Paywall.Infrastructure.Storage;

internal static class SqliteReadWrite
{
    public static string ToText(this Guid value) => value.ToString("N");

    public static string ToText(this DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    public static Guid ReadGuid(this SqliteDataReader reader, int ordinal) =>
        Guid.ParseExact(reader.GetString(ordinal), "N");

    public static string? ReadOptionalText(this SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    public static DateTimeOffset ReadInstant(this SqliteDataReader reader, int ordinal) =>
        DateTimeOffset.Parse(reader.GetString(ordinal), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    public static DateTimeOffset? ReadOptionalInstant(this SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.ReadInstant(ordinal);

    public static void Bind(this SqliteCommand command, string name, object? value) =>
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
}
