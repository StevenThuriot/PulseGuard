using PulseGuard.Entities;
using PulseGuard.Storage.Abstractions.Contracts;
using PulseGuard.Storage.Abstractions.Models;
using TableStorage.Linq;

namespace PulseGuard.Infrastructure;

internal sealed class AzureAgentHistoryStore(PulseContext context) : IAgentHistoryStore
{
    public Task RecordAgentObservationAsync(AgentObservation observation, CancellationToken cancellationToken)
    {
        PulseAgentCheckResult result = new()
        {
            Day = observation.ObservedAt.ToString(PulseAgentCheckResult.PartitionKeyFormat),
            Sqid = observation.Sqid,
            Items = [new PulseAgentCheckResultDetail
            {
                Timestamp = observation.ObservedAt.ToUnixTimeSeconds(),
                Cpu = observation.CpuPercentage,
                Memory = observation.MemoryPercentage,
                InputOutput = observation.InputOutput
            }]
        };

        return context.PulseAgentResults.UpsertEntityAsync(result, cancellationToken);
    }

    public async Task<IReadOnlyList<AgentHistoryRecord>> GetAgentHistoryAsync(string sqid, bool archived, CancellationToken cancellationToken)
    {
        List<PulseAgentCheckResult> results = await context.PulseAgentResults.Where(x => x.Sqid == sqid).OrderBy(x => x.Day).ToListAsync(cancellationToken);
        return [new AgentHistoryRecord(sqid, results.SelectMany(x => x.Items).Select(x => new AgentHistoryItem(DateTimeOffset.FromUnixTimeSeconds(x.Timestamp), x.Cpu, x.Memory, x.InputOutput)).ToList())];
    }
}
