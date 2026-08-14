using Microsoft.Extensions.Options;
using PulseGuard.Entities;
using PulseGuard.Models;
using TableStorage.Linq;

namespace PulseGuard.Infrastructure;

internal sealed class AzureStorageCleanupHostedService(
    IServiceProvider services,
    IOptions<PulseOptions> options,
    ILogger<AzureStorageCleanupHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan interval = TimeSpan.FromMinutes(Math.Max(1, options.Value.CleaningInterval));
        using PeriodicTimer timer = new(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using IServiceScope scope = services.CreateScope();
                PulseContext context = scope.ServiceProvider.GetRequiredService<PulseContext>();
                await CleanAsync(context, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Azure storage cleanup failed.");
            }
        }
    }

    private static async Task CleanAsync(PulseContext context, CancellationToken token)
    {
        DateTimeOffset recent = DateTimeOffset.UtcNow.AddMinutes(-PulseContext.RecentMinutes);
        await context.RecentPulses.Where(x => x.LastUpdatedTimestamp < recent).BatchDeleteAsync(token);

        string today = PulseCheckResult.GetCurrentPartition();
        await foreach (PulseCheckResult pulse in context.PulseCheckResults.Where(x => x.Day != today).WithCancellation(token))
        {
            string year = pulse.Day[..4];
            ArchivedPulseCheckResult archive = await context.ArchivedPulseCheckResults.FindAsync(year, pulse.Sqid, token)
                ?? new ArchivedPulseCheckResult { Year = year, Sqid = pulse.Sqid, Items = [] };
            archive.Group = pulse.Group;
            archive.Name = pulse.Name;
            archive.Items.AddRange(pulse.Items);
            await context.ArchivedPulseCheckResults.UpsertEntityAsync(archive, token);
            await context.PulseCheckResults.DeleteEntityAsync(pulse, token);
            await context.Heatmaps.UpsertEntityAsync(Heatmap.From(pulse), token);
        }

        string agentToday = PulseAgentCheckResult.GetCurrentPartition();
        await foreach (PulseAgentCheckResult pulse in context.PulseAgentResults.Where(x => x.Day != agentToday).WithCancellation(token))
        {
            string year = pulse.Day[..4];
            ArchivedPulseAgentCheckResult archive = await context.ArchivedPulseAgentResults.FindAsync(year, pulse.Sqid, token)
                ?? new ArchivedPulseAgentCheckResult { Year = year, Sqid = pulse.Sqid, Items = [] };
            archive.Items.AddRange(pulse.Items);
            await context.ArchivedPulseAgentResults.UpsertEntityAsync(archive, token);
            await context.PulseAgentResults.DeleteEntityAsync(pulse, token);
        }
    }
}
