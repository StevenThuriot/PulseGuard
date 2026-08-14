using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using PulseGuard.Entities;
using PulseGuard.Models;
using PulseGuard.Storage.Abstractions.Contracts;
using PulseGuard.Storage.Abstractions.Models;
using System.Diagnostics;
using System.Net;
using System.Net.Mime;
using System.Text;
using TableStorage.Linq;

namespace PulseGuard.Routes;

public static class HealthRoutes
{
    private static DateTimeOffset GetOffset(int interval) => DateTimeOffset.UtcNow.AddMinutes(-interval * 2.5);
    private static HttpStatusCode MapToStatusCode(PulseStates state) => state switch
    {
        PulseStates.Healthy or PulseStates.Degraded => HttpStatusCode.OK,
        PulseStates.TimedOut => HttpStatusCode.GatewayTimeout,
        PulseStates.Unknown => HttpStatusCode.NotFound,
        _ => HttpStatusCode.ServiceUnavailable
    };

    extension(IEndpointRouteBuilder builder)
    {
        public void MapHealth()
        {
            builder.MapGet("/version", () => TypedResults.Ok(new AppVersion(AppInfo.Version)))
                   .WithTags("Health")
                   .AllowAnonymous();

            var healthGroup = builder.MapGroup("/health").WithTags("Health");

            healthGroup.MapGet("", async (IMemoryCache cache, IStorageHealthCheck storageHealth, ILogger<Program> logger, CancellationToken token) =>
            {
                PulseStates state = await cache.GetOrCreateAsync("health", async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10);
                    PulseStates state = PulseStates.Unhealthy;

                    try
                    {
                        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                        cts.CancelAfter(5000);

                        var sw = Stopwatch.StartNew();
                        await storageHealth.CheckAsync(cts.Token);

                        state = sw.ElapsedMilliseconds > 1000
                                  ? PulseStates.Degraded
                                  : PulseStates.Healthy;
                    }
                    catch (Exception ex)
                    {
                        logger.FailedHealthChecks(ex);
                        state = PulseStates.TimedOut;
                    }

                    return state;
                });

                HttpStatusCode statusCode = MapToStatusCode(state);
                return TypedResults.Text(state.Stringify(), MediaTypeNames.Text.Plain, Encoding.Default, (int)statusCode);
            })
            .AllowAnonymous();

            healthGroup.MapGet("applications", async (IOptions<PulseOptions> options, IServiceConfigurationStore configurations, IHealthHistoryStore history, CancellationToken token) =>
            {
                IReadOnlyDictionary<string, ServiceIdentifierRecord> uniqueIdentifiers = await configurations.GetServiceIdentifiersAsync(token);

                DateTimeOffset offset = GetOffset(options.Value.Interval);
                Dictionary<string, PulseStates> result = [];
                foreach (ServiceIdentifierRecord identifier in uniqueIdentifiers.Values)
                {
                    PulseStateRecord? current = await history.GetCurrentPulseAsync(identifier.Id, token);
                    if (current is not null && current.LastUpdatedTimestamp > offset)
                    {
                        result[identifier.Group is null ? identifier.Name : $"{identifier.Group}/{identifier.Name}"] = Enum.Parse<PulseStates>(current.State, true);
                    }
                }

                return result.OrderBy(x => x.Key).ToDictionary();
            });

            healthGroup.MapGet("query", async ([FromQuery(Name = "id")] string[] ids, IOptions<PulseOptions> options, IHealthHistoryStore history, CancellationToken token) =>
            {
                if (ids is not { Length: > 0 })
                {
                    return Results.BadRequest();
                }

                DateTimeOffset offset = GetOffset(options.Value.Interval);
                PulseStates state = PulseStates.Unknown;
                foreach (string id in ids)
                {
                    PulseStateRecord? current = await history.GetCurrentPulseAsync(id, token);
                    if (current is not null && current.LastUpdatedTimestamp > offset)
                    {
                        PulseStates candidate = Enum.Parse<PulseStates>(current.State, true);
                        state = state < candidate ? candidate : state;
                    }
                }

                HttpStatusCode code = MapToStatusCode(state);
                return Results.Text(state.Stringify(), MediaTypeNames.Text.Plain, Encoding.Default, (int)code);
            });
        }
    }
}
