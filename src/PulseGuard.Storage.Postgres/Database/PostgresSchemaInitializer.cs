using Microsoft.EntityFrameworkCore;

namespace PulseGuard.Storage.Postgres.Database;

internal sealed class PostgresSchemaInitializer(IDbContextFactory<PulseGuardDbContext> contextFactory)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await context.Database.EnsureCreatedAsync(cancellationToken);
    }
}