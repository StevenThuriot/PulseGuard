using PulseGuard.Entities;
using PulseGuard.Storage.Abstractions.Administration;
using TableStorage.Linq;

namespace PulseGuard.Infrastructure;

internal sealed class AzureConfigurationOverviewStore(PulseContext context) : IConfigurationOverviewStore
{
    public async Task<IReadOnlyList<StoredConfigurationOverview>> GetAsync(CancellationToken cancellationToken)
    {
        List<PulseConfiguration> pulses = await context.Configurations.ToListAsync(cancellationToken);
        List<PulseAgentConfiguration> agents = await context.AgentConfigurations.ToListAsync(cancellationToken);
        Dictionary<string, (string Group, string Name)> identifiers = (await context.Settings.WhereUniqueIdentifier().ToListAsync(cancellationToken)).ToDictionary(x => x.Id, x => (x.Group, x.Name));
        List<StoredConfigurationOverview> result = [];
        result.AddRange(pulses.Where(x => identifiers.ContainsKey(x.Sqid)).Select(x => new StoredConfigurationOverview(x.Sqid, "Normal", x.Type.ToString(), identifiers[x.Sqid].Group, identifiers[x.Sqid].Name, x.Enabled)));
        result.AddRange(agents.Where(x => identifiers.ContainsKey(x.Sqid)).Select(x => new StoredConfigurationOverview(x.Sqid, "Agent", x.Type, identifiers[x.Sqid].Group, identifiers[x.Sqid].Name, x.Enabled)));
        return result;
    }
}
