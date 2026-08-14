using Azure;
using Azure.Data.Tables;
using PulseGuard.Entities;
using PulseGuard.Storage.Abstractions.Administration;
using PulseGuard.Storage.Abstractions.Models;
using TableStorage.Linq;

namespace PulseGuard.Infrastructure;

internal sealed class AzurePulseConfigurationAdministrationStore(PulseContext context) : IPulseConfigurationAdministrationStore
{
    public async Task<StoredPulseConfiguration?> GetAsync(string id, CancellationToken cancellationToken)
    {
        PulseConfiguration? value = await context.Configurations.Where(x => x.Sqid == id).FirstOrDefaultAsync(cancellationToken);
        return value is null ? null : ToStored(value);
    }

    public Task<StorageOperationResult> CreateAsync(CreatePulseConfigurationCommand command, CancellationToken cancellationToken)
        => ExecuteAsync(() => context.Configurations.AddEntityAsync(ToEntity(command.Configuration), cancellationToken));

    public Task<StorageOperationResult> UpdateAsync(UpdatePulseConfigurationCommand command, CancellationToken cancellationToken)
        => ExecuteAsync(() => context.Configurations.UpdateEntityAsync(ToEntity(command.Configuration), ETag.All, TableUpdateMode.Replace, cancellationToken));

    public async Task<StorageOperationResult> SetEnabledAsync(string id, bool enabled, CancellationToken cancellationToken)
    {
        StoredPulseConfiguration? existing = await GetAsync(id, cancellationToken);
        if (existing is null)
        {
            return StorageOperationResult.Missing();
        }

        return await UpdateAsync(new UpdatePulseConfigurationCommand(existing with { Enabled = enabled }), cancellationToken);
    }

    public async Task<StorageOperationResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        StoredPulseConfiguration? existing = await GetAsync(id, cancellationToken);
        if (existing is null)
        {
            return StorageOperationResult.Missing();
        }

        return await ExecuteAsync(() => context.Configurations.DeleteEntityAsync(ToEntity(existing), cancellationToken));
    }

    private static StoredPulseConfiguration ToStored(PulseConfiguration x) => new(x.Sqid, x.Group, x.Name, x.Type.ToString(), x.Location, x.Timeout, x.DegrationTimeout, x.Enabled, x.IgnoreSslErrors, x.ComparisonValue, x.Headers, null, x.AuthenticationId);
    private static PulseConfiguration ToEntity(StoredPulseConfiguration x) => new() { Sqid = x.Sqid, Group = x.Group, Name = x.Name, Type = Enum.Parse<Checks.PulseCheckType>(x.Type, true), Location = x.Location, Timeout = x.Timeout, DegrationTimeout = x.DegradationTimeout, Enabled = x.Enabled, IgnoreSslErrors = x.IgnoreSslErrors, ComparisonValue = x.ComparisonValue, Headers = x.Headers, AuthenticationId = x.CredentialId };

    private static async Task<StorageOperationResult> ExecuteAsync(Func<Task> operation)
    {
        try { await operation(); return StorageOperationResult.Success(); }
        catch (RequestFailedException ex) when (ex.Status == 404) { return StorageOperationResult.Missing(); }
        catch (RequestFailedException ex) when (ex.Status is 409 or 412) { return StorageOperationResult.Conflicted(); }
    }
}

internal sealed class AzureAgentConfigurationAdministrationStore(PulseContext context) : IAgentConfigurationAdministrationStore
{
    public async Task<StoredAgentConfiguration?> GetAsync(string id, string type, CancellationToken cancellationToken)
    {
        PulseAgentConfiguration? value = await context.AgentConfigurations.Where(x => x.Sqid == id && x.Type == type).FirstOrDefaultAsync(cancellationToken);
        return value is null ? null : new StoredAgentConfiguration(value.Sqid, value.Type, value.Location, value.ApplicationName, value.SubscriptionId, value.BuildDefinitionId, value.StageName, value.Enabled, value.Headers, value.AuthenticationId, null);
    }

    public Task<StorageOperationResult> CreateAsync(CreateAgentConfigurationCommand command, CancellationToken cancellationToken)
        => ExecuteAsync(() => context.AgentConfigurations.AddEntityAsync(ToEntity(command.Configuration), cancellationToken));

    public Task<StorageOperationResult> UpdateAsync(UpdateAgentConfigurationCommand command, CancellationToken cancellationToken)
        => ExecuteAsync(() => context.AgentConfigurations.UpdateEntityAsync(ToEntity(command.Configuration), ETag.All, TableUpdateMode.Replace, cancellationToken));

    public async Task<StorageOperationResult> SetEnabledAsync(string id, string type, bool enabled, CancellationToken cancellationToken)
    {
        StoredAgentConfiguration? existing = await GetAsync(id, type, cancellationToken);
        return existing is null ? StorageOperationResult.Missing() : await UpdateAsync(new UpdateAgentConfigurationCommand(existing with { Enabled = enabled }), cancellationToken);
    }

    public async Task<StorageOperationResult> DeleteAsync(string id, string type, CancellationToken cancellationToken)
    {
        StoredAgentConfiguration? existing = await GetAsync(id, type, cancellationToken);
        return existing is null ? StorageOperationResult.Missing() : await ExecuteAsync(() => context.AgentConfigurations.DeleteEntityAsync(ToEntity(existing), cancellationToken));
    }

    private static PulseAgentConfiguration ToEntity(StoredAgentConfiguration x) => new() { Sqid = x.Sqid, Type = x.Type, Location = x.Location, ApplicationName = x.ApplicationName, SubscriptionId = x.SubscriptionId, BuildDefinitionId = x.BuildDefinitionId, StageName = x.StageName, Enabled = x.Enabled, Headers = x.Headers, AuthenticationId = x.CredentialId };
    private static async Task<StorageOperationResult> ExecuteAsync(Func<Task> operation)
    {
        try { await operation(); return StorageOperationResult.Success(); }
        catch (RequestFailedException ex) when (ex.Status == 404) { return StorageOperationResult.Missing(); }
        catch (RequestFailedException ex) when (ex.Status is 409 or 412) { return StorageOperationResult.Conflicted(); }
    }
}
