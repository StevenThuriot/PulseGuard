using Microsoft.Extensions.Hosting;

namespace PulseGuard.Storage.Postgres.Database;

internal sealed class PostgresSchemaHostedService(PostgresSchemaInitializer initializer) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => initializer.InitializeAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
