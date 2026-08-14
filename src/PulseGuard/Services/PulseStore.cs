using Microsoft.Extensions.Options;
using PulseGuard.Models;
using PulseGuard.Storage.Abstractions.Contracts;
using PulseGuard.Storage.Abstractions.Models;

namespace PulseGuard.Services;

public sealed class PulseStore(
    IHealthHistoryStore healthHistory,
    IAgentHistoryStore agentHistory,
    IDeploymentStore deploymentStore,
    IdService idService,
    ILogger<PulseStore> logger)
{
    private readonly IHealthHistoryStore _healthHistory = healthHistory;
    private readonly IAgentHistoryStore _agentHistory = agentHistory;
    private readonly IDeploymentStore _deploymentStore = deploymentStore;
    private readonly IdService _idService = idService;
    private readonly ILogger _logger = logger;

    public Task StoreAsync(PulseReport report, DateTimeOffset creation, long elapsedMilliseconds, CancellationToken token)
    {
        if (string.IsNullOrEmpty(report.Options.Sqid))
        {
            throw new InvalidOperationException($"Pulse '{report.Options.Name}' has no SQID.");
        }

        _logger.StoringPulseReport(report.Options.Sqid, report.Options.Name);
        return _healthHistory.RecordHealthObservationAsync(new(
            Guid.NewGuid().ToString("N"),
            report.Options.Sqid,
            report.Options.Group,
            report.Options.Name,
            report.State.Stringify(),
            creation,
            elapsedMilliseconds,
            report.Message,
            report.Error), token);
    }

    public Task StoreAsync(PulseAgentReport report, DateTimeOffset creation, CancellationToken token)
    {
        _logger.StoringAgentReport(report.Options.Sqid, report.Options.Type);
        return _agentHistory.RecordAgentObservationAsync(new(
            Guid.NewGuid().ToString("N"),
            report.Options.Sqid,
            creation,
            report.CpuPercentage,
            report.Memory,
            report.InputOutput), token);
    }

    public Task StoreAsync(DeploymentAgentReport report, CancellationToken token)
    {
        _logger.StoringDeploymentAgentReport(report.Options.Sqid, report.Options.Type);
        return _deploymentStore.RecordDeploymentAsync(new(
            report.Options.Sqid,
            report.Start,
            report.End,
            report.Status,
            report.Author,
            report.Type,
            report.CommitId,
            report.BuildNumber), token);
    }

    public Task<string> GenerateSqid(string group, string name, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return Task.FromResult(_idService.GetSqid(group, name));
    }
}
