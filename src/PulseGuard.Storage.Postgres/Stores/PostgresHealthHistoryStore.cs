using Microsoft.EntityFrameworkCore;
using PulseGuard.Storage.Abstractions.Contracts;
using PulseGuard.Storage.Abstractions.Models;
using PulseGuard.Storage.Postgres.Database;

namespace PulseGuard.Storage.Postgres.Stores;

internal sealed class PostgresHealthHistoryStore(
    IDbContextFactory<PulseGuardDbContext> contextFactory) : IHealthHistoryStore
{
    public async Task RecordHealthObservationAsync(HealthObservation observation, CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        ServiceEntity service = await context.Services.FirstOrDefaultAsync(x => x.Sqid == observation.Sqid, cancellationToken)
            ?? throw new InvalidOperationException($"Service '{observation.Sqid}' does not exist.");

        Guid eventId = Guid.TryParse(observation.EventId, out Guid parsedEventId) ? parsedEventId : Guid.NewGuid();
        if (!await context.HealthCheckExecutions.AnyAsync(x => x.EventId == eventId, cancellationToken))
        {
            await context.HealthCheckExecutions.AddAsync(new HealthCheckExecutionEntity
            {
                EventId = eventId,
                ServiceId = service.Id,
                ObservedAt = observation.ObservedAt,
                State = observation.State,
                ElapsedMilliseconds = observation.ElapsedMilliseconds,
                Message = observation.Message,
                Error = observation.Error
            }, cancellationToken);
        }

        ServiceStatePeriodEntity? current = await context.StatePeriods
            .Where(x => x.ServiceId == service.Id)
            .OrderByDescending(x => x.LastUpdatedTimestamp)
            .FirstOrDefaultAsync(cancellationToken);

        if (current is null || current.State != observation.State || current.Message != observation.Message || current.Error != observation.Error)
        {
            await context.StatePeriods.AddAsync(new ServiceStatePeriodEntity
            {
                ServiceId = service.Id,
                State = observation.State,
                Message = observation.Message,
                Error = observation.Error,
                CreationTimestamp = observation.ObservedAt,
                LastUpdatedTimestamp = observation.ObservedAt,
                LastElapsedMilliseconds = observation.ElapsedMilliseconds
            }, cancellationToken);
        }
        else
        {
            current.LastUpdatedTimestamp = observation.ObservedAt;
            current.LastElapsedMilliseconds = observation.ElapsedMilliseconds;
        }

        ServiceFailureCounterEntity? counter = await context.FailureCounters.FindAsync([service.Id], cancellationToken);
        counter ??= new ServiceFailureCounterEntity { ServiceId = service.Id };
        counter.ConsecutiveFailures = observation.State is "Healthy" ? 0 : counter.ConsecutiveFailures + 1;
        counter.UpdatedAt = observation.ObservedAt;
        context.Update(counter);

        DateOnly day = DateOnly.FromDateTime(observation.ObservedAt.UtcDateTime);
        ServiceDailyHeatmapEntity? heatmap = await context.Heatmaps.FindAsync([service.Id, day], cancellationToken);
        heatmap ??= new ServiceDailyHeatmapEntity { ServiceId = service.Id, Day = day };
        Increment(heatmap, observation.State);
        context.Update(heatmap);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<PulseStateRecord?> GetCurrentPulseAsync(string sqid, CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.StatePeriods
            .Where(x => x.Service.Sqid == sqid)
            .OrderByDescending(x => x.LastUpdatedTimestamp)
            .Select(x => new PulseStateRecord(x.Service.Sqid, x.State, x.Message, x.Error, x.CreationTimestamp, x.LastUpdatedTimestamp, x.LastElapsedMilliseconds))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PulseStateRecord>> GetPulseHistoryAsync(string sqid, DateTimeOffset? before, int pageSize, CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<ServiceStatePeriodEntity> query = context.StatePeriods.Where(x => x.Service.Sqid == sqid);
        if (before.HasValue)
        {
            query = query.Where(x => x.CreationTimestamp < before.Value);
        }

        return await query.OrderByDescending(x => x.CreationTimestamp)
            .Take(pageSize)
            .Select(x => new PulseStateRecord(x.Service.Sqid, x.State, x.Message, x.Error, x.CreationTimestamp, x.LastUpdatedTimestamp, x.LastElapsedMilliseconds))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HealthHistoryRecord>> GetHealthHistoryAsync(HealthHistoryQuery query, CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<HealthCheckExecutionEntity> executions = context.HealthCheckExecutions.Where(x => x.Service.Sqid == query.Sqid);
        if (query.From.HasValue)
        {
            executions = executions.Where(x => x.ObservedAt >= query.From.Value);
        }

        if (query.To.HasValue)
        {
            executions = executions.Where(x => x.ObservedAt < query.To.Value);
        }

        List<HealthHistoryItem> items = await executions.OrderBy(x => x.ObservedAt)
            .Select(x => new HealthHistoryItem(x.State, x.ObservedAt, x.ElapsedMilliseconds, x.Message, x.Error))
            .ToListAsync(cancellationToken);

        ServiceIdentifierRecord? service = await context.Services.Where(x => x.Sqid == query.Sqid)
            .Select(x => new ServiceIdentifierRecord(x.Sqid, x.GroupName, x.Name))
            .FirstOrDefaultAsync(cancellationToken);

        return service is null ? [] : [new HealthHistoryRecord(service.Id, service.Group ?? string.Empty, service.Name, items)];
    }

    public async Task<IReadOnlyList<HeatmapRecord>> GetHeatmapAsync(string sqid, int limit, CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Heatmaps.Where(x => x.Service.Sqid == sqid)
            .OrderByDescending(x => x.Day)
            .Take(limit)
            .OrderBy(x => x.Day)
            .Select(x => new HeatmapRecord(x.Service.Sqid, x.Day.ToString(), x.UnknownCount, x.HealthyCount, x.DegradedCount, x.UnhealthyCount, x.TimedOutCount))
            .ToListAsync(cancellationToken);
    }

    private static void Increment(ServiceDailyHeatmapEntity heatmap, string state)
    {
        switch (state)
        {
            case "Healthy":
                heatmap.HealthyCount++;
                break;
            case "Degraded":
                heatmap.DegradedCount++;
                break;
            case "Unhealthy":
                heatmap.UnhealthyCount++;
                break;
            case "TimedOut":
                heatmap.TimedOutCount++;
                break;
            default:
                heatmap.UnknownCount++;
                break;
        }
    }
}