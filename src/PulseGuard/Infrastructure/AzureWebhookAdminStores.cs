using Azure;
using Azure.Data.Tables;
using PulseGuard.Entities;
using PulseGuard.Storage.Abstractions.Administration;
using TableStorage.Linq;

namespace PulseGuard.Infrastructure;

internal sealed class AzureWebhookAdministrationStore(PulseContext context) : IWebhookAdministrationStore
{
    public async Task<IReadOnlyList<StoredWebhook>> GetAllAsync(CancellationToken cancellationToken)
        => (await context.Webhooks.ToListAsync(cancellationToken)).Select(ToStored).Where(x => x is not null).Select(x => x!).ToList();

    public async Task<StoredWebhook?> GetAsync(string id, CancellationToken cancellationToken)
        => ToStored(await context.Webhooks.Where(x => x.Id == id).FirstOrDefaultAsync(cancellationToken));

    public Task<StorageOperationResult> CreateAsync(CreateWebhookCommand command, CancellationToken cancellationToken)
        => ExecuteAsync(() => context.Webhooks.AddEntityAsync(ToEntity(command.Webhook), cancellationToken));

    public Task<StorageOperationResult> UpdateAsync(UpdateWebhookCommand command, CancellationToken cancellationToken)
        => ExecuteAsync(() => context.Webhooks.UpdateEntityAsync(ToEntity(command.Webhook), ETag.All, TableUpdateMode.Replace, cancellationToken));

    public async Task<StorageOperationResult> SetEnabledAsync(string id, bool enabled, CancellationToken cancellationToken)
    {
        StoredWebhook? existing = await GetAsync(id, cancellationToken);
        return existing is null ? StorageOperationResult.Missing() : await UpdateAsync(new UpdateWebhookCommand(existing with { Enabled = enabled }), cancellationToken);
    }

    public async Task<StorageOperationResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        StoredWebhook? existing = await GetAsync(id, cancellationToken);
        return existing is null ? StorageOperationResult.Missing() : await ExecuteAsync(() => context.Webhooks.DeleteEntityAsync(ToEntity(existing), cancellationToken));
    }

    private static StoredWebhook? ToStored(Webhook? x) => x is null ? null : new StoredWebhook(x.Id, x.Secret, x.Type.ToString(), x.Group, x.Name, x.Location, x.Enabled, x.AuthenticationId, null);
    private static Webhook ToEntity(StoredWebhook x) => new() { Id = x.Id, Secret = x.Secret, Type = Enum.Parse<WebhookType>(x.Type, true), Group = x.Group, Name = x.Name, Location = x.Location, Enabled = x.Enabled, AuthenticationId = x.CredentialId };
    private static async Task<StorageOperationResult> ExecuteAsync(Func<Task> operation)
    {
        try { await operation(); return StorageOperationResult.Success(); }
        catch (RequestFailedException ex) when (ex.Status == 404) { return StorageOperationResult.Missing(); }
        catch (RequestFailedException ex) when (ex.Status is 409 or 412) { return StorageOperationResult.Conflicted(); }
    }
}
