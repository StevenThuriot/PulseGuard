using Npgsql;
using PulseGuard.Storage.Abstractions.Contracts;

namespace PulseGuard.Storage.Postgres.Database;

internal sealed class PostgresStorageHealthCheck(NpgsqlDataSource dataSource) : IStorageHealthCheck
{
    public async Task CheckAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlCommand command = new("select 1", connection);
        await command.ExecuteScalarAsync(cancellationToken);
    }
}
