using Microsoft.EntityFrameworkCore;
using PulseGuard.Storage.Abstractions.Contracts;
using PulseGuard.Storage.Abstractions.Models;
using PulseGuard.Storage.Postgres.Database;

namespace PulseGuard.Storage.Postgres.Stores;

internal sealed class PostgresWebhookStore(IDbContextFactory<PulseGuardDbContext> contextFactory) : IWebhookStore
{
    public async Task<IReadOnlyList<WebhookRecord>> GetEnabledAsync(CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Webhooks
            .Where(x => x.Enabled)
            .Select(x => new WebhookRecord(x.Id, x.Secret, x.Group, x.Name, x.Location, x.Enabled, x.Type, x.AuthenticationId))
            .ToListAsync(cancellationToken);
    }
}
