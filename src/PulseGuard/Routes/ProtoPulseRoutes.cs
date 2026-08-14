using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Caching.Memory;
using PulseGuard.Entities;
using PulseGuard.Models;
using PulseGuard.Storage.Abstractions.Contracts;
using PulseGuard.Storage.Abstractions.Models;
using System.Data;
using TableStorage.Linq;

namespace PulseGuard.Routes;

public static class ProtoPulseRoutes
{
    extension(IEndpointRouteBuilder builder)
    {
        public void MapProtoPulses()
        {
            builder.MapGroup("/api/1.0/pulses").WithTags("ProtoPulses").CreatePulseMappings();
            builder.MapGroup("/api/1.0/metrics").WithTags("Metrics").CreateMetricsMappings();
        }

        private void CreateMetricsMappings()
        {
            builder.MapGet("{id}", async Task<Results<ProtoResult, NotFound>> (string id, IAgentHistoryStore history, IMemoryCache cache, CancellationToken token) =>
            {
                IReadOnlyList<AgentHistoryRecord> results = await history.GetAgentHistoryAsync(id, false, token);

                if (results.Count is 0)
                {
                    return TypedResults.NotFound();
                }

                var items = results.SelectMany(x => x.Items).Select(x => new PulseAgentCheckResultDetail
                {
                    Timestamp = x.Timestamp.ToUnixTimeSeconds(),
                    Cpu = x.CpuPercentage,
                    Memory = x.MemoryPercentage,
                    InputOutput = x.InputOutput
                });

                PulseMetricsResultGroup result = new(items);
                return Proto.Result(result);
            });

            builder.MapGet("{id}/archived", async Task<Results<ProtoResult, NotFound>> (string id, IAgentHistoryStore history, IMemoryCache cache, CancellationToken token) =>
            {
                IReadOnlyList<AgentHistoryRecord> results = await cache.GetCached($"PulseMetrics-{id}", async () => await history.GetAgentHistoryAsync(id, true, token));
                var archivedItems = results.SelectMany(x => x.Items).Select(x => new PulseAgentCheckResultDetail
                {
                    Timestamp = x.Timestamp.ToUnixTimeSeconds(),
                    Cpu = x.CpuPercentage,
                    Memory = x.MemoryPercentage,
                    InputOutput = x.InputOutput
                }).ToList();

                PulseMetricsResultGroup result = new(archivedItems);
                return Proto.ImmutableResult(result);
            });
        }

        private void CreatePulseMappings()
        {
            builder.MapGet("details/{id}", async Task<Results<ProtoResult, NotFound>> (string id, IServiceConfigurationStore configurations, IHealthHistoryStore history, IMemoryCache cache, CancellationToken token) =>
            {
                ServiceIdentifierRecord? info = await configurations.GetServiceIdentifierAsync(id, token);

                if (info is null)
                {
                    return TypedResults.NotFound();
                }

                IReadOnlyList<HealthHistoryRecord> results = await history.GetHealthHistoryAsync(new(id), token);

                if (results.Count is 0)
                {
                    return TypedResults.NotFound();
                }

                var items = results.SelectMany(x => x.Items).Select(x => new PulseCheckResultDetail
                {
                    State = Enum.Parse<PulseStates>(x.State, true),
                    Timestamp = x.Timestamp.ToUnixTimeSeconds(),
                    ElapsedMilliseconds = x.ElapsedMilliseconds
                });

                PulseDetailResultGroup result = new(info.Group ?? string.Empty, info.Name, items);

                return Proto.Result(result);
            });

            builder.MapGet("details/{id}/archived", async Task<Results<ProtoResult, NotFound>> (string id, IServiceConfigurationStore configurations, IHealthHistoryStore history, IMemoryCache cache, CancellationToken token) =>
            {
                ServiceIdentifierRecord? info = await configurations.GetServiceIdentifierAsync(id, token);

                if (info is null)
                {
                    return TypedResults.NotFound();
                }

                IReadOnlyList<HealthHistoryRecord> results = await cache.GetCached($"PulseDetals-{id}", async () => await history.GetHealthHistoryAsync(new(id), token));
                var archivedItems = results.SelectMany(x => x.Items).Select(x => new PulseCheckResultDetail
                {
                    State = Enum.Parse<PulseStates>(x.State, true),
                    Timestamp = x.Timestamp.ToUnixTimeSeconds(),
                    ElapsedMilliseconds = x.ElapsedMilliseconds
                }).ToList();

                PulseDetailResultGroup result = new(info.Group ?? string.Empty, info.Name, archivedItems);

                return Proto.ImmutableResult(result);
            });

            builder.MapGet("heatmap/{id}", async Task<Results<NotFound, ProtoResult>> (string id, IHealthHistoryStore history, CancellationToken token) =>
            {
                IReadOnlyList<HeatmapRecord> heatmaps = await history.GetHeatmapAsync(id, 370, token);
                List<PulseHeatmap> entries = [.. heatmaps.Select(x => new PulseHeatmap(x.Day, x.Unknown, x.Healthy, x.Degraded, x.Unhealthy, x.TimedOut))];

                if (entries.Count is 0)
                {
                    return TypedResults.NotFound();
                }

                return Proto.Result(new PulseHeatmaps(id, entries));
            });
        }
    }

    private static Task<T> GetCached<T>(this IMemoryCache cache, string key, Func<ValueTask<T>> factory)
        => cache.GetOrCreateAsync(key, x =>
        {
            x.AbsoluteExpiration = DateTimeOffset.UtcNow.Date.AddDays(1).AddMinutes(-5);
            return factory().AsTask();
        })!;

    private static ValueTask<T> AsValue<T>(this Task<T> task) => new(task);
}
