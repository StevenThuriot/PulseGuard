namespace PulseGuard.Storage.Abstractions.Administration;

public enum StorageOperationStatus
{
    Succeeded,
    NotFound,
    Conflict
}

public readonly record struct StorageOperationResult(StorageOperationStatus Status)
{
    public bool Succeeded => Status is StorageOperationStatus.Succeeded;
    public bool NotFound => Status is StorageOperationStatus.NotFound;
    public bool Conflict => Status is StorageOperationStatus.Conflict;

    public static StorageOperationResult Success() => new(StorageOperationStatus.Succeeded);
    public static StorageOperationResult Missing() => new(StorageOperationStatus.NotFound);
    public static StorageOperationResult Conflicted() => new(StorageOperationStatus.Conflict);
}

public sealed record StoredCredential(
    string Id,
    string Type,
    string? Username,
    string? Secret,
    string? Header,
    string? TokenEndpoint,
    string? ClientId,
    string? Scopes);

public sealed record StoredUser(
    string UserId,
    string? Nickname,
    IReadOnlyList<string> Roles,
    DateTimeOffset? LastVisited,
    string? ConcurrencyToken = null);

public sealed record StoredPulseConfiguration(
    string Sqid,
    string Group,
    string Name,
    string Type,
    string Location,
    int Timeout,
    int? DegradationTimeout,
    bool Enabled,
    bool IgnoreSslErrors,
    string? ComparisonValue,
    string? Headers,
    string? CredentialType,
    string? CredentialId,
    string? ConcurrencyToken = null);

public sealed record StoredAgentConfiguration(
    string Sqid,
    string Type,
    string Location,
    string? ApplicationName,
    string? SubscriptionId,
    int? BuildDefinitionId,
    string? StageName,
    bool Enabled,
    string? Headers,
    string? CredentialType,
    string? CredentialId,
    string? ConcurrencyToken = null);

public sealed record StoredWebhook(
    string Id,
    string Secret,
    string Type,
    string Group,
    string Name,
    string Location,
    bool Enabled,
    string? CredentialType,
    string? CredentialId,
    string? ConcurrencyToken = null);

public sealed record StoredServiceIdentifier(
    string Id,
    string Group,
    string Name,
    string? ConcurrencyToken = null);

public sealed record StoredConfigurationOverview(
    string Id,
    string Type,
    string SubType,
    string Group,
    string Name,
    bool Enabled);
