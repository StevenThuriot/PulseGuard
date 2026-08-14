namespace PulseGuard.Storage.Postgres.Database;

internal sealed class ServiceEntity
{
    public long Id { get; set; }
    public string Sqid { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public PulseConfigurationEntity? PulseConfiguration { get; set; }
}

internal sealed class PulseConfigurationEntity
{
    public long ServiceId { get; set; }
    public string Location { get; set; } = string.Empty;
    public string CheckType { get; set; } = string.Empty;
    public int TimeoutMilliseconds { get; set; }
    public int? DegradationTimeoutMilliseconds { get; set; }
    public bool Enabled { get; set; }
    public bool IgnoreSslErrors { get; set; }
    public string? ComparisonValue { get; set; }
    public string? Headers { get; set; }
    public string? AuthenticationId { get; set; }
    public string? SubscriptionId { get; set; }
    public int? BuildDefinitionId { get; set; }
    public string? StageName { get; set; }
    public ServiceEntity Service { get; set; } = null!;
}

internal sealed class AgentConfigurationEntity
{
    public long Id { get; set; }
    public string Sqid { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string? ApplicationName { get; set; }
    public bool Enabled { get; set; }
    public string? Headers { get; set; }
    public string? AuthenticationId { get; set; }
    public string? SubscriptionId { get; set; }
    public int? BuildDefinitionId { get; set; }
    public string? StageName { get; set; }
}

internal sealed class HealthCheckExecutionEntity
{
    public long Id { get; set; }
    public Guid EventId { get; set; }
    public long ServiceId { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
    public string State { get; set; } = string.Empty;
    public long ElapsedMilliseconds { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
    public ServiceEntity Service { get; set; } = null!;
}

internal sealed class AgentExecutionEntity
{
    public long Id { get; set; }
    public Guid EventId { get; set; }
    public string Sqid { get; set; } = string.Empty;
    public DateTimeOffset ObservedAt { get; set; }
    public double? CpuPercentage { get; set; }
    public double? MemoryPercentage { get; set; }
    public double? InputOutput { get; set; }
}

internal sealed class ServiceStatePeriodEntity
{
    public long Id { get; set; }
    public long ServiceId { get; set; }
    public string State { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreationTimestamp { get; set; }
    public DateTimeOffset LastUpdatedTimestamp { get; set; }
    public long? LastElapsedMilliseconds { get; set; }
    public ServiceEntity Service { get; set; } = null!;
}

internal sealed class ServiceFailureCounterEntity
{
    public long ServiceId { get; set; }
    public int ConsecutiveFailures { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ServiceEntity Service { get; set; } = null!;
}

internal sealed class ServiceDailyHeatmapEntity
{
    public long ServiceId { get; set; }
    public DateOnly Day { get; set; }
    public int UnknownCount { get; set; }
    public int HealthyCount { get; set; }
    public int DegradedCount { get; set; }
    public int UnhealthyCount { get; set; }
    public int TimedOutCount { get; set; }
    public ServiceEntity Service { get; set; } = null!;
}

internal sealed class DeploymentEntity
{
    public long Id { get; set; }
    public long ServiceId { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset? EndAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Author { get; set; }
    public string? Type { get; set; }
    public string? CommitId { get; set; }
    public string? BuildNumber { get; set; }
    public ServiceEntity Service { get; set; } = null!;
}
