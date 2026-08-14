using Microsoft.EntityFrameworkCore;
using PulseGuard.Storage.Abstractions.Contracts;
using PulseGuard.Storage.Abstractions.Models;
using PulseGuard.Storage.Postgres.Database;

namespace PulseGuard.Storage.Postgres.Stores;

internal sealed class PostgresCredentialStore(IDbContextFactory<PulseGuardDbContext> contextFactory) : ICredentialStore
{
    public async Task<CredentialRecord?> GetAsync(string id, string type, CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Credentials
            .Where(x => x.Id == id && x.Type == type)
            .Select(x => new CredentialRecord(x.Id, x.Type, x.Username, x.Secret, x.Header, x.TokenEndpoint, x.ClientId, x.Scopes))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
