using Microsoft.EntityFrameworkCore;
using PulseGuard.Storage.Abstractions.Administration;
using PulseGuard.Storage.Postgres.Database;

namespace PulseGuard.Storage.Postgres.Stores;

internal sealed class PostgresConfigurationOverviewStore(IDbContextFactory<PulseGuardDbContext> factory) : IConfigurationOverviewStore
{
    public async Task<IReadOnlyList<StoredConfigurationOverview>> GetAsync(CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await factory.CreateDbContextAsync(cancellationToken);
        List<StoredConfigurationOverview> pulses = await context.PulseConfigurations.Select(x => new StoredConfigurationOverview(x.Service.Sqid, "Normal", x.CheckType, x.Service.GroupName, x.Service.Name, x.Enabled)).ToListAsync(cancellationToken);
        List<StoredConfigurationOverview> agents = await context.AgentConfigurations.Select(x => new StoredConfigurationOverview(x.Sqid, "Agent", x.Type, string.Empty, x.ApplicationName ?? string.Empty, x.Enabled)).ToListAsync(cancellationToken);
        return pulses.Concat(agents).ToList();
    }
}
