using PulseGuard.Entities;
using PulseGuard.Storage.Abstractions.Contracts;
using PulseGuard.Storage.Abstractions.Models;
using TableStorage.Linq;

namespace PulseGuard.Infrastructure;

internal sealed class AzureServiceConfigurationStore(PulseContext context) : IServiceConfigurationStore
{
    public async Task<IReadOnlyList<PulseConfigurationRecord>> GetPulseConfigurationsAsync(bool enabledOnly, CancellationToken cancellationToken)
    {
        List<PulseConfiguration> configurations = enabledOnly
            ? await context.Configurations.Where(x => x.Enabled).ToListAsync(cancellationToken)
            : await context.Configurations.ToListAsync(cancellationToken);

        return configurations.Select(x => new PulseConfigurationRecord(
                x.Group,
                x.Name,
                x.Location,
                x.Type.ToString(),
                x.Timeout,
                x.DegrationTimeout,
                x.Enabled,
                x.IgnoreSslErrors,
                x.Sqid,
                x.ComparisonValue,
                x.Headers,
                x.AuthenticationId)).ToList();
    }

    public async Task<IReadOnlyList<AgentConfigurationRecord>> GetAgentConfigurationsAsync(bool enabledOnly, CancellationToken cancellationToken)
    {
        List<PulseAgentConfiguration> configurations = enabledOnly
            ? await context.AgentConfigurations.Where(x => x.Enabled).ToListAsync(cancellationToken)
            : await context.AgentConfigurations.ToListAsync(cancellationToken);

        return configurations.Select(x => new AgentConfigurationRecord(
                x.Sqid,
                x.Type,
                x.Location,
                x.ApplicationName,
                x.SubscriptionId,
                x.BuildDefinitionId,
                x.StageName,
                x.Enabled,
                x.Headers,
                x.AuthenticationId)).ToList();
    }

    public async Task<IReadOnlyDictionary<string, ServiceIdentifierRecord>> GetServiceIdentifiersAsync(CancellationToken cancellationToken)
    {
        List<UniqueIdentifier> identifiers = await context.Settings.WhereUniqueIdentifier()
            .ToListAsync(cancellationToken);

        return identifiers
            .Select(x => new ServiceIdentifierRecord(x.Id, x.Group, x.Name))
            .ToDictionary(x => x.Id);
    }

    public async Task<ServiceIdentifierRecord?> GetServiceIdentifierAsync(string id, CancellationToken cancellationToken)
    {
        UniqueIdentifier? identifier = await context.Settings.FindUniqueIdentifierAsync(id, cancellationToken);
        return identifier is null ? null : new ServiceIdentifierRecord(identifier.Id, identifier.Group, identifier.Name);
    }
}
