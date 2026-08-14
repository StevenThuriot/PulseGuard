using PulseGuard.Storage.Abstractions.Administration;

namespace PulseGuard.Services.Admin;

public sealed class WebhookAdministrationService(IWebhookAdministrationStore store)
{
    public Task<IReadOnlyList<StoredWebhook>> GetAllAsync(CancellationToken cancellationToken)
        => store.GetAllAsync(cancellationToken);

    public Task<StoredWebhook?> GetAsync(string id, CancellationToken cancellationToken)
        => store.GetAsync(id, cancellationToken);

    public Task<StorageOperationResult> CreateAsync(StoredWebhook webhook, CancellationToken cancellationToken)
        => store.CreateAsync(new CreateWebhookCommand(webhook), cancellationToken);

    public Task<StorageOperationResult> UpdateAsync(StoredWebhook webhook, CancellationToken cancellationToken)
        => store.UpdateAsync(new UpdateWebhookCommand(webhook), cancellationToken);

    public Task<StorageOperationResult> SetEnabledAsync(string id, bool enabled, CancellationToken cancellationToken)
        => store.SetEnabledAsync(id, enabled, cancellationToken);

    public Task<StorageOperationResult> DeleteAsync(string id, CancellationToken cancellationToken)
        => store.DeleteAsync(id, cancellationToken);
}
