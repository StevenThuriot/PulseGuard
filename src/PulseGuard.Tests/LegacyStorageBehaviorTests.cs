using PulseGuard.Entities;
using PulseGuard.Models;
using PulseGuard.Storage.Abstractions.Contracts;
using PulseGuard.Storage.Abstractions.Administration;
using PulseGuard.Storage.Abstractions.Queues;
using PulseGuard.Storage.Abstractions.Models;
using Xunit;

namespace PulseGuard.Tests;

public sealed class LegacyStorageBehaviorTests
{
    [Fact]
    public void PulseContinuationToken_RoundTripsUnixTimestamp()
    {
        const long timestamp = 1_750_000_000;

        string token = Pulse.CreateContinuationToken(DateTimeOffset.FromUnixTimeSeconds(timestamp));

        Assert.Equal(timestamp, Pulse.ConvertToUnixTimeSeconds(token));
    }

    [Fact]
    public void PulseCheckResult_AppendValueUsesDayAndSqidPartitioning()
    {
        DateTimeOffset creation = new(2026, 8, 14, 12, 30, 0, TimeSpan.Zero);
        PulseReport report = new(
            new PulseConfiguration
            {
                Sqid = "abc123",
                Group = "group",
                Name = "name"
            },
            PulseStates.Healthy,
            "ok",
            null);

        (string partition, string row, BinaryData data) = PulseCheckResult.GetAppendValue(report, creation, 42);

        Assert.Equal("20260814", partition);
        Assert.Equal("abc123", row);
        Assert.Contains("42", data.ToString());
    }

    [Fact]
    public void StorageContracts_DoNotExposeProviderSpecificDeliveryTypes()
    {
        Assert.Equal(typeof(object), typeof(StorageQueueMessage).GetProperty(nameof(StorageQueueMessage.DeliveryHandle))!.PropertyType);
        Assert.NotNull(typeof(IHealthHistoryStore).GetMethod(nameof(IHealthHistoryStore.RecordHealthObservationAsync)));
    }

    [Fact]
    public void AgentConfigurationRecord_PreservesExternalAgentFields()
    {
        AgentConfigurationRecord record = new(
            "sqid",
            "WebAppDeployment",
            "resource-group",
            "application",
            "subscription",
            42,
            "stage",
            true,
            null,
            null);

        Assert.Equal("subscription", record.SubscriptionId);
        Assert.Equal(42, record.BuildDefinitionId);
        Assert.Equal("stage", record.StageName);
    }

    [Fact]
    public void AdminStorageResult_ModelsProviderIndependentOutcomes()
    {
        Assert.True(StorageOperationResult.Success().Succeeded);
        Assert.True(StorageOperationResult.Missing().NotFound);
        Assert.True(StorageOperationResult.Conflicted().Conflict);
    }

    [Fact]
    public void AdminContracts_DoNotExposeAzureOrEfTypes()
    {
        Assert.DoesNotContain(typeof(ICredentialAdministrationStore).GetMethods(), method =>
            method.ToString()!.Contains("PulseContext", StringComparison.Ordinal));
        Assert.DoesNotContain(typeof(IWebhookAdministrationStore).GetMethods(), method =>
            method.ToString()!.Contains("DbContext", StringComparison.Ordinal));
    }
}
