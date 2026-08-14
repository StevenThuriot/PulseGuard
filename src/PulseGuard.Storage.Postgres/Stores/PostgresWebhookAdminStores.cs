using Microsoft.EntityFrameworkCore;
using PulseGuard.Storage.Abstractions.Administration;
using PulseGuard.Storage.Postgres.Database;
using System.Linq.Expressions;

namespace PulseGuard.Storage.Postgres.Stores;

internal sealed class PostgresWebhookAdministrationStore(IDbContextFactory<PulseGuardDbContext> factory) : IWebhookAdministrationStore
{
    public async Task<IReadOnlyList<StoredWebhook>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Webhooks.Select(ToRecord()).ToListAsync(cancellationToken);
    }

    public async Task<StoredWebhook?> GetAsync(string id, CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Webhooks.Where(x => x.Id == id).Select(ToRecord()).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<StorageOperationResult> CreateAsync(CreateWebhookCommand command, CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await factory.CreateDbContextAsync(cancellationToken);
        if (await context.Webhooks.AnyAsync(x => x.Id == command.Webhook.Id, cancellationToken))
        {
            return StorageOperationResult.Conflicted();
        }

        await context.Webhooks.AddAsync(ToEntity(command.Webhook), cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return StorageOperationResult.Success();
    }

    public async Task<StorageOperationResult> UpdateAsync(UpdateWebhookCommand command, CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await factory.CreateDbContextAsync(cancellationToken);
        WebhookEntity? entity = await context.Webhooks.FirstOrDefaultAsync(x => x.Id == command.Webhook.Id, cancellationToken);
        if (entity is null)
        {
            return StorageOperationResult.Missing();
        }

        Copy(command.Webhook, entity);
        await context.SaveChangesAsync(cancellationToken);
        return StorageOperationResult.Success();
    }

    public async Task<StorageOperationResult> SetEnabledAsync(string id, bool enabled, CancellationToken cancellationToken)
    {
        StoredWebhook? existing = await GetAsync(id, cancellationToken);
        return existing is null ? StorageOperationResult.Missing() : await UpdateAsync(new UpdateWebhookCommand(existing with { Enabled = enabled }), cancellationToken);
    }

    public async Task<StorageOperationResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await factory.CreateDbContextAsync(cancellationToken);
        WebhookEntity? entity = await context.Webhooks.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return StorageOperationResult.Missing();
        }

        context.Webhooks.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
        return StorageOperationResult.Success();
    }

    private static Expression<Func<WebhookEntity, StoredWebhook>> ToRecord() => x => new StoredWebhook(x.Id, x.Secret, x.Type, x.Group, x.Name, x.Location, x.Enabled, x.AuthenticationId, null);
    private static WebhookEntity ToEntity(StoredWebhook x) => new() { Id = x.Id, Secret = x.Secret, Type = x.Type, Group = x.Group, Name = x.Name, Location = x.Location, Enabled = x.Enabled, AuthenticationId = x.CredentialId };
    private static void Copy(StoredWebhook x, WebhookEntity entity) { entity.Secret = x.Secret; entity.Type = x.Type; entity.Group = x.Group; entity.Name = x.Name; entity.Location = x.Location; entity.Enabled = x.Enabled; entity.AuthenticationId = x.CredentialId; }
}

internal sealed class PostgresServiceIdentifierAdministrationStore(IDbContextFactory<PulseGuardDbContext> factory) : IServiceIdentifierAdministrationStore
{
    public async Task<StoredServiceIdentifier?> GetAsync(string id, CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Services.Where(x => x.Sqid == id).Select(x => new StoredServiceIdentifier(x.Sqid, x.GroupName, x.Name)).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<StoredServiceIdentifier?> ReserveAsync(string group, string name, CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await factory.CreateDbContextAsync(cancellationToken);
        string id = Guid.CreateVersion7().ToString("N");
        if (await context.Services.AnyAsync(x => x.GroupName == group && x.Name == name, cancellationToken))
        {
            return null;
        }

        ServiceEntity service = new() { Sqid = id, GroupName = group, Name = name };
        await context.Services.AddAsync(service, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return new StoredServiceIdentifier(id, group, name);
    }

    public async Task<StorageOperationResult> UpdateAsync(UpdateServiceIdentifierCommand command, CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await factory.CreateDbContextAsync(cancellationToken);
        ServiceEntity? service = await context.Services.FirstOrDefaultAsync(x => x.Sqid == command.Identifier.Id, cancellationToken);
        if (service is null)
        {
            return StorageOperationResult.Missing();
        }

        service.GroupName = command.Identifier.Group;
        service.Name = command.Identifier.Name;
        await context.SaveChangesAsync(cancellationToken);
        return StorageOperationResult.Success();
    }
}
