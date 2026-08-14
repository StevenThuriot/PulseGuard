using Azure.Storage.Queues;
using Microsoft.Extensions.Options;
using PulseGuard.Storage.Abstractions.Contracts;
using PulseGuard.Storage.Azure.Configuration;

namespace PulseGuard.Storage.Azure.Configuration;

internal sealed class AzureStorageHealthCheck(IOptions<AzureStorageOptions> options) : IStorageHealthCheck
{
    public Task CheckAsync(CancellationToken cancellationToken)
    {
        QueueClient client = new(options.Value.ConnectionString, "pulseguard-health-check");
        return client.ExistsAsync(cancellationToken);
    }
}
