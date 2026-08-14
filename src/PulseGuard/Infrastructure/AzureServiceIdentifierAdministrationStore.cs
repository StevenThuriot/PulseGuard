using Azure;
using Azure.Data.Tables;
using PulseGuard.Entities;
using PulseGuard.Services;
using PulseGuard.Storage.Abstractions.Administration;
using TableStorage.Linq;

namespace PulseGuard.Infrastructure;

internal sealed class AzureServiceIdentifierAdministrationStore(PulseContext context, IdService idService) : IServiceIdentifierAdministrationStore
{
    public async Task<StoredServiceIdentifier?> GetAsync(string id, CancellationToken cancellationToken)
    {
        UniqueIdentifier? value = await context.Settings.FindUniqueIdentifierAsync(id, cancellationToken);
        return value is null ? null : new StoredServiceIdentifier(value.Id, value.Group, value.Name);
    }

    public async Task<StoredServiceIdentifier?> ReserveAsync(string group, string name, CancellationToken cancellationToken)
    {
        string id = idService.GetSqid(group, name);
        for (int attempt = 0; attempt < 11; attempt++)
        {
            try
            {
                await context.Settings.AddEntityAsync(new UniqueIdentifier { Id = id, Group = group, Name = name }, cancellationToken);
                return new StoredServiceIdentifier(id, group, name);
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                id = idService.GetRandomSqid();
            }
        }

        return null;
    }

    public async Task<StorageOperationResult> UpdateAsync(UpdateServiceIdentifierCommand command, CancellationToken cancellationToken)
    {
        try
        {
            UniqueIdentifier? existing = await context.Settings.FindUniqueIdentifierAsync(command.Identifier.Id, cancellationToken);
            if (existing is null)
            {
                return StorageOperationResult.Missing();
            }

            existing.Group = command.Identifier.Group;
            existing.Name = command.Identifier.Name;
            await context.Settings.UpdateEntityAsync(existing, ETag.All, TableUpdateMode.Replace, cancellationToken);
            return StorageOperationResult.Success();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return StorageOperationResult.Missing();
        }
        catch (RequestFailedException ex) when (ex.Status is 409 or 412)
        {
            return StorageOperationResult.Conflicted();
        }
    }
}
