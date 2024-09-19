﻿using Microsoft.Extensions.Options;
using PulseGuard.Checks;
using PulseGuard.Entities;
using PulseGuard.Models;
using System.Diagnostics;
using TableStorage.Linq;

namespace PulseGuard.Services.Hosted;

public sealed class PulseHostedService(IServiceProvider services, IOptions<PulseOptions> options, ILogger<PulseHostedService> logger) : BackgroundService
{
    private readonly IServiceProvider _services = services;
    private readonly PulseOptions _options = options.Value;
    private readonly ILogger<PulseHostedService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int interval = _options.Interval;
        TimeSpan maxExecution = TimeSpan.FromMinutes(interval) - TimeSpan.FromSeconds(5);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                DateTime now = DateTime.UtcNow;
                DateTime next = new(now.Year, now.Month, now.Day, now.Hour, 0, 0);
                next = next.AddMinutes(((now.Minute / interval) + 1) * interval);
                await Task.Delay(next - now, stoppingToken);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                cts.CancelAfter(maxExecution);

                await CheckPulseAsync(cts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(PulseEventIds.HealthChecks, ex, "Error checking pulse");
            }
        }
    }

    private async Task CheckPulseAsync(CancellationToken token)
    {
        using var scope = _services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<PulseContext>();
        var configurations = await context.Configurations.Where(c => c.Enabled).ToListAsync(token);

        var store = scope.ServiceProvider.GetRequiredService<PulseStore>();
        var factory = scope.ServiceProvider.GetRequiredService<PulseCheckFactory>();
        var checks = new Task[configurations.Count];

        using SemaphoreSlim semaphore = new(_options.SimultaneousPulses, _options.SimultaneousPulses); // rate gate

        for (int i = 0; i < checks.Length; i++)
        {
            PulseConfiguration configuration = configurations[i];
            checks[i] = Task.Run(() => Check(configuration), token);
        }

        await Task.WhenAll(checks);

        async Task Check(PulseConfiguration config)
        {
            try
            {
                await semaphore.WaitAsync(token);

                PulseCheck check = factory.Create(config);
                await CheckPulseAsync(check, store, token);
            }
            catch (Exception ex)
            {
                _logger.LogError(PulseEventIds.HealthChecks, ex, "Error checking pulse");
            }
            finally
            {
                semaphore.Release();
            }
        }
    }

    private async Task CheckPulseAsync(PulseCheck check, PulseStore store, CancellationToken token)
    {
        PulseReport? report = null;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(check.Options.Timeout);

            var sw = Stopwatch.StartNew();

            report = await check.CheckAsync(cts.Token);
            report = PostProcessReport(report, sw);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(PulseEventIds.HealthChecks, ex, "Pulse timeout");
            report = PulseReport.Fail(check.Options, "Pulse check failed due timeout", $"Pulse timed out after {check.Options.Timeout}ms");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(PulseEventIds.HealthChecks, ex, "HTTP Error checking pulse");

            string error = ex.Message;
            if (ex.InnerException?.Message is not null)
            {
                error = $"{error.TrimEnd('.', ' ')}: {ex.InnerException.Message.TrimEnd('.', ' ')}";
            }

            report = PulseReport.Fail(check.Options, "Pulse check failed due to http request exception", error);
        }
        catch (Exception ex)
        {
            _logger.LogError(PulseEventIds.HealthChecks, ex, "Error checking pulse");
            report = PulseReport.Fail(check.Options, "Pulse check failed due to exception", ex.Message);
        }
        finally
        {
            if (report is not null)
            {
                await store.StoreAsync(report, token);
            }
        }
    }

    private static PulseReport PostProcessReport(PulseReport report, Stopwatch sw)
    {
        if (report.State is PulseStates.Healthy)
        {
            int? degrationTimeout = report.Options.DegrationTimeout;
            if (degrationTimeout.HasValue && sw.ElapsedMilliseconds > degrationTimeout.GetValueOrDefault())
            {
                return report with
                {
                    State = PulseStates.Degraded,
                    Message = $"Pulse check took longer than the expected {degrationTimeout.GetValueOrDefault()}ms",
                    Error = $"Pulse degraded because it took {sw.ElapsedMilliseconds}ms to complete"
                };
            }
        }

        return report;
    }
}