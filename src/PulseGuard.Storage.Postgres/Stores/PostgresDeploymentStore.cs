using Microsoft.EntityFrameworkCore;
using PulseGuard.Storage.Abstractions.Contracts;
using PulseGuard.Storage.Abstractions.Models;
using PulseGuard.Storage.Postgres.Database;

namespace PulseGuard.Storage.Postgres.Stores;

internal sealed class PostgresDeploymentStore(IDbContextFactory<PulseGuardDbContext> contextFactory) : IDeploymentStore
{
    public async Task RecordDeploymentAsync(DeploymentObservation observation, CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        ServiceEntity service = await context.Services.FirstAsync(x => x.Sqid == observation.Sqid, cancellationToken);
        await context.Deployments.AddAsync(new DeploymentEntity
        {
            ServiceId = service.Id,
            StartAt = observation.Start,
            EndAt = observation.End,
            Status = observation.Status,
            Author = observation.Author,
            Type = observation.Type,
            CommitId = observation.CommitId,
            BuildNumber = observation.BuildNumber
        }, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DeploymentObservation>> GetDeploymentsAsync(string sqid, CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Deployments.Where(x => x.Service.Sqid == sqid).OrderByDescending(x => x.StartAt)
            .Select(x => new DeploymentObservation(x.Service.Sqid, x.StartAt, x.EndAt, x.Status, x.Author, x.Type, x.CommitId, x.BuildNumber))
            .ToListAsync(cancellationToken);
    }
}
