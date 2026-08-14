using Azure;
using Azure.Data.Tables;
using PulseGuard.Entities;
using PulseGuard.Models;
using PulseGuard.Storage.Abstractions.Contracts;
using PulseGuard.Storage.Abstractions.Models;
using TableStorage.Linq;

namespace PulseGuard.Infrastructure;

internal sealed class AzureHealthHistoryStore(PulseContext context) : IHealthHistoryStore
{
    public async Task RecordHealthObservationAsync(HealthObservation observation, CancellationToken cancellationToken)
    {
        Pulse pulse = await context.Pulses.Where(x => x.Sqid == observation.Sqid).FirstOrDefaultAsync(cancellationToken)
            ?? new Pulse
            {
                Sqid = observation.Sqid,
                ContinuationToken = Pulse.CreateContinuationToken(observation.ObservedAt),
                State = Enum.Parse<PulseStates>(observation.State, true),
                Message = observation.Message ?? string.Empty,
                Error = observation.Error,
                CreationTimestamp = observation.ObservedAt,
                LastUpdatedTimestamp = observation.ObservedAt
            };

        pulse.State = Enum.Parse<PulseStates>(observation.State, true);
        pulse.Message = observation.Message ?? string.Empty;
        pulse.Error = observation.Error;
        pulse.LastUpdatedTimestamp = observation.ObservedAt;
        pulse.LastElapsedMilliseconds = observation.ElapsedMilliseconds;
        await context.Pulses.UpsertEntityAsync(pulse, TableUpdateMode.Replace, cancellationToken);
        await context.RecentPulses.UpsertEntityAsync(pulse, TableUpdateMode.Replace, cancellationToken);

        string day = observation.ObservedAt.ToString(PulseCheckResult.PartitionKeyFormat);
        string value = PulseCheckResultDetails.Separator + PulseCheckResultDetail.Serialize(
            Enum.Parse<PulseStates>(observation.State, true),
            observation.ObservedAt.ToUnixTimeSeconds(),
            observation.ElapsedMilliseconds);
        await context.PulseCheckResults.AppendAsync(day, observation.Sqid, BinaryData.FromString(value).ToStream(), cancellationToken);
    }

    public async Task<PulseStateRecord?> GetCurrentPulseAsync(string sqid, CancellationToken cancellationToken)
    {
        Pulse? pulse = await context.Pulses.Where(x => x.Sqid == sqid).OrderByDescending(x => x.CreationTimestamp).FirstOrDefaultAsync(cancellationToken);
        return pulse is null ? null : ToRecord(pulse);
    }

    public async Task<IReadOnlyList<PulseStateRecord>> GetPulseHistoryAsync(string sqid, DateTimeOffset? before, int pageSize, CancellationToken cancellationToken)
    {
        var query = context.Pulses.Where(x => x.Sqid == sqid);
        if (before.HasValue)
        {
            query = query.Where(x => x.CreationTimestamp < before.Value);
        }

        return (await query.OrderByDescending(x => x.CreationTimestamp).Take(pageSize).ToListAsync(cancellationToken)).Select(ToRecord).ToList();
    }

    public async Task<IReadOnlyList<HealthHistoryRecord>> GetHealthHistoryAsync(HealthHistoryQuery query, CancellationToken cancellationToken)
    {
        List<PulseCheckResult> results = await context.PulseCheckResults.Where(x => x.Sqid == query.Sqid).OrderBy(x => x.Day).ToListAsync(cancellationToken);
        return results.Count is 0 ? [] : [new HealthHistoryRecord(query.Sqid, results[0].Group, results[0].Name, results.SelectMany(x => x.Items).Select(x => new HealthHistoryItem(x.State.Stringify(), DateTimeOffset.FromUnixTimeSeconds(x.Timestamp), x.ElapsedMilliseconds, null, null)).ToList())];
    }

    public async Task<IReadOnlyList<HeatmapRecord>> GetHeatmapAsync(string sqid, int limit, CancellationToken cancellationToken)
    {
        return (await context.Heatmaps.Where(x => x.Sqid == sqid).OrderByDescending(x => x.Day).Take(limit).ToListAsync(cancellationToken)).Select(x => new HeatmapRecord(x.Sqid, x.Day, x.Unknown, x.Healthy, x.Degraded, x.Unhealthy, x.TimedOut)).ToList();
    }

    private static PulseStateRecord ToRecord(Pulse pulse) => new(pulse.Sqid, pulse.State.Stringify(), pulse.Message, pulse.Error, pulse.CreationTimestamp, pulse.LastUpdatedTimestamp, pulse.LastElapsedMilliseconds);
}
