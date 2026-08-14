using Microsoft.EntityFrameworkCore;
using PulseGuard.Storage.Abstractions.Contracts;
using PulseGuard.Storage.Abstractions.Models;
using PulseGuard.Storage.Postgres.Database;

namespace PulseGuard.Storage.Postgres.Stores;

internal sealed class PostgresServiceConfigurationStore(
    IDbContextFactory<PulseGuardDbContext> contextFactory) : IServiceConfigurationStore
{
    public async Task<IReadOnlyList<PulseConfigurationRecord>> GetPulseConfigurationsAsync(
        bool enabledOnly,
        CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<PulseConfigurationEntity> query = context.PulseConfigurations.Include(x => x.Service);

        if (enabledOnly)
        {
            query = query.Where(x => x.Enabled);
        }

        return await query.Select(x => new PulseConfigurationRecord(
                x.Service.GroupName,
                x.Service.Name,
                x.Location,
                x.CheckType,
                x.TimeoutMilliseconds,
                x.DegradationTimeoutMilliseconds,
                x.Enabled,
                x.IgnoreSslErrors,
                x.Service.Sqid,
                x.ComparisonValue,
                x.Headers,
                x.AuthenticationId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AgentConfigurationRecord>> GetAgentConfigurationsAsync(
        bool enabledOnly,
        CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<AgentConfigurationEntity> query = context.AgentConfigurations;

        if (enabledOnly)
        {
            query = query.Where(x => x.Enabled);
        }

        return await query.Select(x => new AgentConfigurationRecord(
                x.Sqid,
                x.Type,
                x.Location,
                x.ApplicationName,
                x.SubscriptionId,
                x.BuildDefinitionId,
                x.StageName,
                x.Enabled,
                x.Headers,
                x.AuthenticationId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, ServiceIdentifierRecord>> GetServiceIdentifiersAsync(
        CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Services
            .Select(x => new ServiceIdentifierRecord(x.Sqid, x.GroupName, x.Name))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
    }

    public async Task<ServiceIdentifierRecord?> GetServiceIdentifierAsync(
        string id,
        CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Services
            .Where(x => x.Sqid == id)
            .Select(x => new ServiceIdentifierRecord(x.Sqid, x.GroupName, x.Name))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
