using PulseGuard.Entities;
using PulseGuard.Storage.Abstractions.Contracts;
using PulseGuard.Storage.Abstractions.Models;
using TableStorage.Linq;

namespace PulseGuard.Infrastructure;

internal sealed class AzureDeploymentStore(PulseContext context) : IDeploymentStore
{
    public Task RecordDeploymentAsync(DeploymentObservation observation, CancellationToken cancellationToken)
    {
        return context.Deployments.UpsertEntityAsync(new DeploymentResult
        {
            Sqid = observation.Sqid,
            ContinuationToken = Pulse.CreateContinuationToken(observation.Start),
            Start = observation.Start,
            End = observation.End,
            Status = observation.Status,
            Author = observation.Author,
            Type = observation.Type,
            CommitId = observation.CommitId,
            BuildNumber = observation.BuildNumber
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<DeploymentObservation>> GetDeploymentsAsync(string sqid, CancellationToken cancellationToken)
    {
        return await context.Deployments.Where(x => x.Sqid == sqid).OrderByDescending(x => x.Start).Select(x => new DeploymentObservation(x.Sqid, x.Start, x.End, x.Status, x.Author, x.Type, x.CommitId, x.BuildNumber)).ToListAsync(cancellationToken);
    }
}
