namespace PulseGuard.Storage.Abstractions.Models;

public sealed record ServiceIdentifierRecord(string Id, string? Group, string Name);

public sealed record PulseConfigurationRecord(
    string Group,
    string Name,
    string Location,
    string Type,
    int TimeoutMilliseconds,
    int? DegradationTimeoutMilliseconds,
    bool Enabled,
    bool IgnoreSslErrors,
    string? Sqid,
    string? ComparisonValue,
    string? Headers,
    string? AuthenticationId);

public sealed record AgentConfigurationRecord(
    string Sqid,
    string Type,
    string Location,
    string? ApplicationName,
    string? SubscriptionId,
    int? BuildDefinitionId,
    string? StageName,
    bool Enabled,
    string? Headers,
    string? AuthenticationId);

public sealed record HealthObservation(
    string EventId,
    string Sqid,
    string Group,
    string Name,
    string State,
    DateTimeOffset ObservedAt,
    long ElapsedMilliseconds,
    string? Message,
    string? Error);

public sealed record AgentObservation(
    string EventId,
    string Sqid,
    DateTimeOffset ObservedAt,
    double? CpuPercentage,
    double? MemoryPercentage,
    double? InputOutput);

public sealed record DeploymentObservation(
    string Sqid,
    DateTimeOffset Start,
    DateTimeOffset? End,
    string Status,
    string? Author,
    string? Type,
    string? CommitId,
    string? BuildNumber);

public sealed record PulseStateRecord(
    string Sqid,
    string State,
    string? Message,
    string? Error,
    DateTimeOffset CreationTimestamp,
    DateTimeOffset LastUpdatedTimestamp,
    long? LastElapsedMilliseconds);

public sealed record FailureCounterRecord(string Sqid, int Value);

public sealed record HeatmapRecord(
    string Sqid,
    string Day,
    int Unknown,
    int Healthy,
    int Degraded,
    int Unhealthy,
    int TimedOut);

public sealed record CredentialRecord(
    string Id,
    string Type,
    string? Username,
    string? Secret,
    string? Header,
    string? TokenEndpoint,
    string? ClientId,
    string? Scopes);

public sealed record WebhookRecord(
    string Id,
    string Secret,
    string Group,
    string Name,
    string Location,
    bool Enabled,
    string Type,
    string? AuthenticationId);

public sealed record UserRecord(
    string UserId,
    string? Nickname,
    IReadOnlyList<string> Roles,
    DateTimeOffset? LastVisited);

public sealed record HealthHistoryQuery(
    string Sqid,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null);

public sealed record HealthHistoryRecord(
    string Sqid,
    string Group,
    string Name,
    IReadOnlyList<HealthHistoryItem> Items);

public sealed record HealthHistoryItem(
    string State,
    DateTimeOffset Timestamp,
    long? ElapsedMilliseconds,
    string? Message,
    string? Error);

public sealed record AgentHistoryRecord(
    string Sqid,
    IReadOnlyList<AgentHistoryItem> Items);

public sealed record AgentHistoryItem(
    DateTimeOffset Timestamp,
    double? CpuPercentage,
    double? MemoryPercentage,
    double? InputOutput);
