using PulseGuard.Storage.Abstractions.Models;

namespace PulseGuard.Storage.Abstractions.Contracts;

public interface IServiceConfigurationStore
{
    public Task<IReadOnlyList<PulseConfigurationRecord>> GetPulseConfigurationsAsync(bool enabledOnly, CancellationToken cancellationToken);

    public Task<IReadOnlyList<AgentConfigurationRecord>> GetAgentConfigurationsAsync(bool enabledOnly, CancellationToken cancellationToken);

    public Task<IReadOnlyDictionary<string, ServiceIdentifierRecord>> GetServiceIdentifiersAsync(CancellationToken cancellationToken);

    public Task<ServiceIdentifierRecord?> GetServiceIdentifierAsync(string id, CancellationToken cancellationToken);
}

public interface IHealthHistoryStore
{
    public Task RecordHealthObservationAsync(HealthObservation observation, CancellationToken cancellationToken);

    public Task<PulseStateRecord?> GetCurrentPulseAsync(string sqid, CancellationToken cancellationToken);

    public Task<IReadOnlyList<PulseStateRecord>> GetPulseHistoryAsync(string sqid, DateTimeOffset? before, int pageSize, CancellationToken cancellationToken);

    public Task<IReadOnlyList<HealthHistoryRecord>> GetHealthHistoryAsync(HealthHistoryQuery query, CancellationToken cancellationToken);

    public Task<IReadOnlyList<HeatmapRecord>> GetHeatmapAsync(string sqid, int limit, CancellationToken cancellationToken);
}

public interface IAgentHistoryStore
{
    public Task RecordAgentObservationAsync(AgentObservation observation, CancellationToken cancellationToken);

    public Task<IReadOnlyList<AgentHistoryRecord>> GetAgentHistoryAsync(string sqid, bool archived, CancellationToken cancellationToken);
}

public interface IDeploymentStore
{
    public Task RecordDeploymentAsync(DeploymentObservation observation, CancellationToken cancellationToken);

    public Task<IReadOnlyList<DeploymentObservation>> GetDeploymentsAsync(string sqid, CancellationToken cancellationToken);
}

public interface ICredentialStore
{
    public Task<CredentialRecord?> GetAsync(string id, string type, CancellationToken cancellationToken);
}

public interface IWebhookStore
{
    public Task<IReadOnlyList<WebhookRecord>> GetEnabledAsync(CancellationToken cancellationToken);
}

public interface IStorageMaintenance
{
    public Task RunAsync(CancellationToken cancellationToken);
}

public interface IStorageHealthCheck
{
    public Task CheckAsync(CancellationToken cancellationToken);
}
