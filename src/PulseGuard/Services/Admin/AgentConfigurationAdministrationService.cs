using PulseGuard.Storage.Abstractions.Administration;

namespace PulseGuard.Services.Admin;

public sealed class AgentConfigurationAdministrationService(IAgentConfigurationAdministrationStore store)
{
    public Task<StoredAgentConfiguration?> GetAsync(string id, string type, CancellationToken cancellationToken)
        => store.GetAsync(id, type, cancellationToken);

    public Task<StorageOperationResult> CreateAsync(StoredAgentConfiguration configuration, CancellationToken cancellationToken)
        => store.CreateAsync(new CreateAgentConfigurationCommand(configuration), cancellationToken);

    public Task<StorageOperationResult> UpdateAsync(StoredAgentConfiguration configuration, CancellationToken cancellationToken)
        => store.UpdateAsync(new UpdateAgentConfigurationCommand(configuration), cancellationToken);

    public Task<StorageOperationResult> SetEnabledAsync(string id, string type, bool enabled, CancellationToken cancellationToken)
        => store.SetEnabledAsync(id, type, enabled, cancellationToken);

    public Task<StorageOperationResult> DeleteAsync(string id, string type, CancellationToken cancellationToken)
        => store.DeleteAsync(id, type, cancellationToken);
}
