using Microsoft.EntityFrameworkCore;
using PulseGuard.Storage.Abstractions.Contracts;
using PulseGuard.Storage.Abstractions.Models;
using PulseGuard.Storage.Postgres.Database;

namespace PulseGuard.Storage.Postgres.Stores;

internal sealed class PostgresAgentHistoryStore(IDbContextFactory<PulseGuardDbContext> contextFactory) : IAgentHistoryStore
{
    public async Task RecordAgentObservationAsync(AgentObservation observation, CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        Guid eventId = Guid.TryParse(observation.EventId, out Guid parsed) ? parsed : Guid.NewGuid();
        if (!await context.AgentExecutions.AnyAsync(x => x.EventId == eventId, cancellationToken))
        {
            await context.AgentExecutions.AddAsync(new AgentExecutionEntity
            {
                EventId = eventId,
                Sqid = observation.Sqid,
                ObservedAt = observation.ObservedAt,
                CpuPercentage = observation.CpuPercentage,
                MemoryPercentage = observation.MemoryPercentage,
                InputOutput = observation.InputOutput
            }, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<AgentHistoryRecord>> GetAgentHistoryAsync(string sqid, bool archived, CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        List<AgentHistoryItem> items = await context.AgentExecutions.Where(x => x.Sqid == sqid)
            .OrderBy(x => x.ObservedAt)
            .Select(x => new AgentHistoryItem(x.ObservedAt, x.CpuPercentage, x.MemoryPercentage, x.InputOutput))
            .ToListAsync(cancellationToken);
        return [new AgentHistoryRecord(sqid, items)];
    }
}
