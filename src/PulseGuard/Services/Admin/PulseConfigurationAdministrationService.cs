using PulseGuard.Storage.Abstractions.Administration;

namespace PulseGuard.Services.Admin;

public sealed class PulseConfigurationAdministrationService(
    IPulseConfigurationAdministrationStore store,
    IServiceIdentifierAdministrationStore identifiers)
{
    public Task<StoredPulseConfiguration?> GetAsync(string id, CancellationToken cancellationToken)
        => store.GetAsync(id, cancellationToken);

    public Task<StorageOperationResult> CreateAsync(StoredPulseConfiguration configuration, CancellationToken cancellationToken)
        => store.CreateAsync(new CreatePulseConfigurationCommand(configuration), cancellationToken);

    public Task<StorageOperationResult> UpdateAsync(StoredPulseConfiguration configuration, CancellationToken cancellationToken)
        => store.UpdateAsync(new UpdatePulseConfigurationCommand(configuration), cancellationToken);

    public Task<StorageOperationResult> SetEnabledAsync(string id, bool enabled, CancellationToken cancellationToken)
        => store.SetEnabledAsync(id, enabled, cancellationToken);

    public Task<StorageOperationResult> DeleteAsync(string id, CancellationToken cancellationToken)
        => store.DeleteAsync(id, cancellationToken);

    public Task<StoredServiceIdentifier?> ReserveIdentifierAsync(string group, string name, CancellationToken cancellationToken)
        => identifiers.ReserveAsync(group, name, cancellationToken);

    public Task<StorageOperationResult> UpdateIdentifierAsync(StoredServiceIdentifier identifier, CancellationToken cancellationToken)
        => identifiers.UpdateAsync(new UpdateServiceIdentifierCommand(identifier), cancellationToken);
}
