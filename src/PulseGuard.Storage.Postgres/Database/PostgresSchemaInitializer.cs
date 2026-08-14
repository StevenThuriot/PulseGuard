using Npgsql;
using System.Globalization;

namespace PulseGuard.Storage.Postgres.Database;

internal sealed class PostgresSchemaInitializer(NpgsqlDataSource dataSource)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlCommand command = new(PostgresSchema.Sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);

        DateTime month = new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        await CreatePartitionsAsync(connection, "health_check_executions", month, cancellationToken);
        await CreatePartitionsAsync(connection, "agent_executions", month, cancellationToken);
        await CreatePartitionsAsync(connection, "health_check_executions", month.AddMonths(1), cancellationToken);
        await CreatePartitionsAsync(connection, "agent_executions", month.AddMonths(1), cancellationToken);
    }

    private static async Task CreatePartitionsAsync(
        NpgsqlConnection connection,
        string table,
        DateTime month,
        CancellationToken cancellationToken)
    {
        string suffix = month.ToString("yyyyMM", CultureInfo.InvariantCulture);
        string start = month.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string end = month.AddMonths(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string partition = $"{table}_{suffix}";

        string sql = $"create table if not exists {partition} partition of {table} for values from ('{start}') to ('{end}')";
        await using NpgsqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
