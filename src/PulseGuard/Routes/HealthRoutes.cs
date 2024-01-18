using Microsoft.Extensions.Caching.Memory;
using PulseGuard.Entities;
using PulseGuard.Models;
using System.Diagnostics;
using System.Net.Mime;
using System.Text;
using TableStorage.Linq;

namespace PulseGuard.Routes;

public static class HealthRoutes
{
    public static void MapHealth(this WebApplication app)
    {
        app.MapGet("/health", async (IMemoryCache cache, PulseContext context, ILogger<Program> logger, CancellationToken token) =>
        {
            (PulseStates state, int statusCode) = await cache.GetOrCreateAsync("health", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10);
                PulseStates state = PulseStates.Unhealthy;
                int statusCode = 200;

                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    cts.CancelAfter(5000);

                    var sw = Stopwatch.StartNew();
                    _ = await context.Configurations.FirstOrDefaultAsync(cts.Token);

                    state = sw.ElapsedMilliseconds > 1000
                              ? PulseStates.Degraded
                              : PulseStates.Healthy;

                    statusCode = 200;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed health checks");
                    state = PulseStates.Unhealthy;
                    statusCode = 503;
                }

                return (state, statusCode);
            });

            return TypedResults.Text(state.Stringify(), MediaTypeNames.Text.Plain, Encoding.Default, statusCode);
        });
    }
}