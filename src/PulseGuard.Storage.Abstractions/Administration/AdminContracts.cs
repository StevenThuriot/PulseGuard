namespace PulseGuard.Storage.Abstractions.Administration;

public interface ICredentialAdministrationStore
{
    public Task<IReadOnlyList<StoredCredential>> GetAllAsync(CancellationToken cancellationToken);
    public Task<StoredCredential?> GetAsync(string type, string id, CancellationToken cancellationToken);
    public Task<StorageOperationResult> CreateAsync(CreateCredentialCommand command, CancellationToken cancellationToken);
    public Task<StorageOperationResult> UpdateAsync(UpdateCredentialCommand command, CancellationToken cancellationToken);
    public Task<StorageOperationResult> DeleteAsync(string type, string id, CancellationToken cancellationToken);
}

public interface IUserAdministrationStore
{
    public Task<IReadOnlyList<StoredUser>> GetAllAsync(CancellationToken cancellationToken);
    public Task<StoredUser?> GetAsync(string id, CancellationToken cancellationToken);
    public Task<StorageOperationResult> CreateAsync(CreateUserCommand command, CancellationToken cancellationToken);
    public Task<StorageOperationResult> UpdateAsync(UpdateUserCommand command, CancellationToken cancellationToken);
    public Task<StorageOperationResult> DeleteAsync(string id, CancellationToken cancellationToken);
}

public interface IPulseConfigurationAdministrationStore
{
    public Task<StoredPulseConfiguration?> GetAsync(string id, CancellationToken cancellationToken);
    public Task<StorageOperationResult> CreateAsync(CreatePulseConfigurationCommand command, CancellationToken cancellationToken);
    public Task<StorageOperationResult> UpdateAsync(UpdatePulseConfigurationCommand command, CancellationToken cancellationToken);
    public Task<StorageOperationResult> SetEnabledAsync(string id, bool enabled, CancellationToken cancellationToken);
    public Task<StorageOperationResult> DeleteAsync(string id, CancellationToken cancellationToken);
}

public interface IAgentConfigurationAdministrationStore
{
    public Task<StoredAgentConfiguration?> GetAsync(string id, string type, CancellationToken cancellationToken);
    public Task<StorageOperationResult> CreateAsync(CreateAgentConfigurationCommand command, CancellationToken cancellationToken);
    public Task<StorageOperationResult> UpdateAsync(UpdateAgentConfigurationCommand command, CancellationToken cancellationToken);
    public Task<StorageOperationResult> SetEnabledAsync(string id, string type, bool enabled, CancellationToken cancellationToken);
    public Task<StorageOperationResult> DeleteAsync(string id, string type, CancellationToken cancellationToken);
}

public interface IWebhookAdministrationStore
{
    public Task<IReadOnlyList<StoredWebhook>> GetAllAsync(CancellationToken cancellationToken);
    public Task<StoredWebhook?> GetAsync(string id, CancellationToken cancellationToken);
    public Task<StorageOperationResult> CreateAsync(CreateWebhookCommand command, CancellationToken cancellationToken);
    public Task<StorageOperationResult> UpdateAsync(UpdateWebhookCommand command, CancellationToken cancellationToken);
    public Task<StorageOperationResult> SetEnabledAsync(string id, bool enabled, CancellationToken cancellationToken);
    public Task<StorageOperationResult> DeleteAsync(string id, CancellationToken cancellationToken);
}

public interface IServiceIdentifierAdministrationStore
{
    public Task<StoredServiceIdentifier?> GetAsync(string id, CancellationToken cancellationToken);
    public Task<StoredServiceIdentifier?> ReserveAsync(string group, string name, CancellationToken cancellationToken);
    public Task<StorageOperationResult> UpdateAsync(UpdateServiceIdentifierCommand command, CancellationToken cancellationToken);
}

public interface IConfigurationOverviewStore
{
    public Task<IReadOnlyList<StoredConfigurationOverview>> GetAsync(CancellationToken cancellationToken);
}
