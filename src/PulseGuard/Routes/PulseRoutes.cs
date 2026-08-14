using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PulseGuard.Entities;
using PulseGuard.Models;
using PulseGuard.Storage.Abstractions.Contracts;
using PulseGuard.Storage.Abstractions.Models;

namespace PulseGuard.Routes;

public static class PulseRoutes
{
    extension(IEndpointRouteBuilder builder)
    {
        public void MapPulses()
        {
            RouteGroupBuilder group = builder.MapGroup("/api/1.0/pulses").WithTags("Pulses");

            group.MapGet("", async (IHealthHistoryStore history, IServiceConfigurationStore configurations, CancellationToken token, [FromQuery] uint? minutes = null) =>
            {
                uint minuteOffset = minutes ?? 720;
                DateTimeOffset offset = DateTimeOffset.UtcNow.AddMinutes(-minuteOffset);
                IReadOnlyDictionary<string, ServiceIdentifierRecord> identifiers = await configurations.GetServiceIdentifiersAsync(token);
                List<HealthHistoryRecord> records = [];
                foreach (ServiceIdentifierRecord identifier in identifiers.Values)
                {
                    IReadOnlyList<HealthHistoryRecord> historyRecords = await history.GetHealthHistoryAsync(new(identifier.Id, offset, null), token);
                    records.AddRange(historyRecords);
                }

                return records.GroupBy(x => x.Group)
                    .Select(group => new PulseOverviewGroup(group.Key, group.Select(x => new PulseOverviewGroupItem(
                        x.Sqid,
                        x.Name,
                        x.Items.Select(item => new PulseOverviewItem(Enum.Parse<PulseStates>(item.State, true), item.Message ?? string.Empty, item.Timestamp, item.Timestamp)).ToList()))))
                    .ToList();
            });

            group.MapGet("application/{id}", async Task<Results<Ok<PulseDetailGroupItem>, NotFound>> (string id, IHealthHistoryStore history, IServiceConfigurationStore configurations, CancellationToken token, [FromQuery] string? continuationToken = null, [FromQuery] int pageSize = 10) =>
            {
                ServiceIdentifierRecord? identifier = await configurations.GetServiceIdentifierAsync(id, token);

                if (identifier is null)
                {
                    return TypedResults.NotFound();
                }

                DateTimeOffset? before = string.IsNullOrEmpty(continuationToken) ? null : DateTimeOffset.FromUnixTimeSeconds(Pulse.ConvertToUnixTimeSeconds(continuationToken));
                IReadOnlyList<PulseStateRecord> items = await history.GetPulseHistoryAsync(id, before, pageSize, token);

                if (items.Count is 0)
                {
                    return TypedResults.NotFound();
                }

                var entries = items.Select(x => new PulseDetailItem(Enum.Parse<PulseStates>(x.State, true), x.Message ?? string.Empty, x.CreationTimestamp, x.LastUpdatedTimestamp, x.Error));

                PulseStateRecord pulse = items[^1];

                continuationToken = items.Count < pageSize
                                         ? null
                                         : Pulse.CreateContinuationToken(pulse.CreationTimestamp);

                PulseDetailGroupItem result = new(pulse.Sqid, identifier.Name, continuationToken, entries);
                return TypedResults.Ok(result);
            });

            group.MapGet("application/{id}/deployments", async Task<Results<NotFound, Ok<PulseDeployments>>> (string id, IDeploymentStore deploymentsStore, CancellationToken token) =>
            {
                IReadOnlyList<DeploymentObservation> deployments = await deploymentsStore.GetDeploymentsAsync(id, token);

                if (deployments.Count is 0)
                {
                    return TypedResults.NotFound();
                }

                return TypedResults.Ok(new PulseDeployments(id, deployments.Select(x => new PulseDeployment(x.Status, x.Start, x.End, x.Author, x.Type, x.CommitId, x.BuildNumber)).ToList()));
            });
        }
    }
}
