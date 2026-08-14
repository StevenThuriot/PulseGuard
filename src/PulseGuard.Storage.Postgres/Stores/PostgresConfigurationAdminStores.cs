using Microsoft.EntityFrameworkCore;
using PulseGuard.Storage.Abstractions.Administration;
using PulseGuard.Storage.Postgres.Database;

namespace PulseGuard.Storage.Postgres.Stores;

internal sealed class PostgresPulseConfigurationAdministrationStore(IDbContextFactory<PulseGuardDbContext> factory) : IPulseConfigurationAdministrationStore
{
    public async Task<StoredPulseConfiguration?> GetAsync(string id, CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.PulseConfigurations.Where(x => x.Service.Sqid == id).Select(x => new StoredPulseConfiguration(x.Service.Sqid, x.Service.GroupName, x.Service.Name, x.CheckType, x.Location, x.TimeoutMilliseconds, x.DegradationTimeoutMilliseconds, x.Enabled, x.IgnoreSslErrors, x.ComparisonValue, x.Headers, null, null)).FirstOrDefaultAsync(cancellationToken);
    }

    public Task<StorageOperationResult> CreateAsync(CreatePulseConfigurationCommand command, CancellationToken cancellationToken) => WriteAsync(command.Configuration, false, cancellationToken);
    public Task<StorageOperationResult> UpdateAsync(UpdatePulseConfigurationCommand command, CancellationToken cancellationToken) => WriteAsync(command.Configuration, true, cancellationToken);

    public async Task<StorageOperationResult> SetEnabledAsync(string id, bool enabled, CancellationToken cancellationToken)
    {
        StoredPulseConfiguration? existing = await GetAsync(id, cancellationToken);
        return existing is null ? StorageOperationResult.Missing() : await WriteAsync(existing with { Enabled = enabled }, true, cancellationToken);
    }

    public async Task<StorageOperationResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await factory.CreateDbContextAsync(cancellationToken);
        ServiceEntity? service = await context.Services.FirstOrDefaultAsync(x => x.Sqid == id, cancellationToken);
        if (service is null)
        {
            return StorageOperationResult.Missing();
        }

        context.Services.Remove(service);
        await context.SaveChangesAsync(cancellationToken);
        return StorageOperationResult.Success();
    }

    private async Task<StorageOperationResult> WriteAsync(StoredPulseConfiguration value, bool update, CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await factory.CreateDbContextAsync(cancellationToken);
        ServiceEntity? service = await context.Services.FirstOrDefaultAsync(x => x.Sqid == value.Sqid, cancellationToken);
        if (!update && service is not null)
        {
            return StorageOperationResult.Conflicted();
        }

        if (update && service is null)
        {
            return StorageOperationResult.Missing();
        }

        service ??= new ServiceEntity { Sqid = value.Sqid };
        service.GroupName = value.Group;
        service.Name = value.Name;
        service.PulseConfiguration ??= new PulseConfigurationEntity { Service = service };
        PulseConfigurationEntity configuration = service.PulseConfiguration;
        configuration.Location = value.Location;
        configuration.CheckType = value.Type;
        configuration.TimeoutMilliseconds = value.Timeout;
        configuration.DegradationTimeoutMilliseconds = value.DegradationTimeout;
        configuration.Enabled = value.Enabled;
        configuration.IgnoreSslErrors = value.IgnoreSslErrors;
        configuration.ComparisonValue = value.ComparisonValue;
        configuration.Headers = value.Headers;
        context.Update(service);
        await context.SaveChangesAsync(cancellationToken);
        return StorageOperationResult.Success();
    }
}

internal sealed class PostgresAgentConfigurationAdministrationStore(IDbContextFactory<PulseGuardDbContext> factory) : IAgentConfigurationAdministrationStore
{
    public async Task<StoredAgentConfiguration?> GetAsync(string id, string type, CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.AgentConfigurations.Where(x => x.Sqid == id && x.Type == type).Select(x => new StoredAgentConfiguration(x.Sqid, x.Type, x.Location, x.ApplicationName, x.SubscriptionId, x.BuildDefinitionId, x.StageName, x.Enabled, x.Headers, null, null)).FirstOrDefaultAsync(cancellationToken);
    }

    public Task<StorageOperationResult> CreateAsync(CreateAgentConfigurationCommand command, CancellationToken cancellationToken) => WriteAsync(command.Configuration, false, cancellationToken);
    public Task<StorageOperationResult> UpdateAsync(UpdateAgentConfigurationCommand command, CancellationToken cancellationToken) => WriteAsync(command.Configuration, true, cancellationToken);

    public async Task<StorageOperationResult> SetEnabledAsync(string id, string type, bool enabled, CancellationToken cancellationToken)
    {
        StoredAgentConfiguration? existing = await GetAsync(id, type, cancellationToken);
        return existing is null ? StorageOperationResult.Missing() : await WriteAsync(existing with { Enabled = enabled }, true, cancellationToken);
    }

    public async Task<StorageOperationResult> DeleteAsync(string id, string type, CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await factory.CreateDbContextAsync(cancellationToken);
        AgentConfigurationEntity? entity = await context.AgentConfigurations.FirstOrDefaultAsync(x => x.Sqid == id && x.Type == type, cancellationToken);
        if (entity is null)
        {
            return StorageOperationResult.Missing();
        }

        context.AgentConfigurations.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
        return StorageOperationResult.Success();
    }

    private async Task<StorageOperationResult> WriteAsync(StoredAgentConfiguration value, bool update, CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await factory.CreateDbContextAsync(cancellationToken);
        AgentConfigurationEntity? entity = await context.AgentConfigurations.FirstOrDefaultAsync(x => x.Sqid == value.Sqid && x.Type == value.Type, cancellationToken);
        if (!update && entity is not null)
        {
            return StorageOperationResult.Conflicted();
        }

        if (update && entity is null)
        {
            return StorageOperationResult.Missing();
        }

        entity ??= new AgentConfigurationEntity { Sqid = value.Sqid, Type = value.Type };
        entity.Location = value.Location;
        entity.ApplicationName = value.ApplicationName;
        entity.SubscriptionId = value.SubscriptionId;
        entity.BuildDefinitionId = value.BuildDefinitionId;
        entity.StageName = value.StageName;
        entity.Enabled = value.Enabled;
        entity.Headers = value.Headers;
        context.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
        return StorageOperationResult.Success();
    }
}
