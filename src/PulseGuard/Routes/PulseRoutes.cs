using Microsoft.AspNetCore.Mvc;
using PulseGuard.Entities;
using PulseGuard.Models;
using System.Data;
using System.Net.Mime;
using TableStorage.Linq;
using System.Linq;

namespace PulseGuard.Routes;

public static class PulseRoutes
{
    public static void MapPulses(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/1.0/pulses");

        group.MapGet("", (PulseContext context, [FromQuery] uint? minutes = null) =>
        {
            long cappedNegativeMinutes = -1L * (minutes ?? 720);
            DateTimeOffset offset = DateTimeOffset.UtcNow.AddMinutes(cappedNegativeMinutes);

            return context.Pulses
                          .Where(x => x.Timestamp > offset)
                          .SelectFields(x => new { x.Sqid, x.Group, x.Name, x.Message, x.State, x.CreationTimestamp, x.Timestamp })
                          .GroupBy(x => x.Group ?? "")
                          .Select(group =>
                              new PulseOverviewGroup(group.Key, group.GroupBy(x => new { x.Sqid, x.Name })
                                                                     .Select(pulses =>
                                                                     {
                                                                         var entries = pulses.Select(x => new PulseOverviewItem(x.State, x.Message, x.CreationTimestamp, x.Timestamp));
                                                                         return new PulseOverviewGroupItem(pulses.Key.Sqid, pulses.Key.Name, entries);
                                                                     }))
                          );
        });

        group.MapGet("application/{id}", async (string id, PulseContext context, CancellationToken token, [FromQuery] string? continuationToken = null, [FromQuery] int pageSize = 10) =>
        {
            var query = context.Pulses.Where(x => x.Sqid == id);

            if (!string.IsNullOrEmpty(continuationToken))
            {
                long creationTimeSeconds = Pulse.ConvertToUnixTimeSeconds(continuationToken);
                var creationTimestamp = DateTimeOffset.FromUnixTimeSeconds(creationTimeSeconds);
                query = query.Where(x => x.CreationTimestamp < creationTimestamp);
            }

            List<Pulse> items = await query.Take(pageSize)
                                           .AsAsyncEnumerable().Take(10) //TODO: Fix this in TableStorage as Take is only setting the page size but the TableClient will automatically request the next page
                                           .ToListAsync(token);

            if (items.Count == 0)
            {
                return Results.NotFound();
            }

            var entries = items.Select(x => new PulseDetailItem(x.State, x.Message, x.CreationTimestamp, x.Timestamp, x.Error));

            Pulse pulse = items[^1];

            continuationToken = items.Count < pageSize
                                     ? null
                                     : pulse.ContinuationToken;

            return Results.Ok(new PulseDetailGroupItem(pulse.Sqid, pulse.Name, continuationToken, entries.ToAsyncEnumerable()));
        });

        group.MapGet("application/{id}/state", async (string id, PulseContext context, CancellationToken token) =>
        {
            Pulse? pulse = await context.Pulses.Where(x => x.Sqid == id)
                                               .SelectFields(x => new { x.State, x.CreationTimestamp })
                                               .FirstOrDefaultAsync(token);

            PulseStates state = pulse?.State ?? PulseStates.Unknown;

            int statusCode = state switch
            {
                PulseStates.Healthy => 200,
                PulseStates.Degraded => 218,
                PulseStates.Unknown => 404,
                _ => 503
            };

            return TypedResults.Text(state.Stringify(), contentType: MediaTypeNames.Text.Plain, statusCode: statusCode);
        });
    }
}