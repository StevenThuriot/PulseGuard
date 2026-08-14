namespace PulseGuard.Storage.Abstractions.Queues;

public enum StorageQueueName
{
    Pulses,
    Agents,
    Webhooks
}

public sealed record StorageQueueMessage(
    string MessageId,
    DateTimeOffset EnqueuedAt,
    ReadOnlyMemory<byte> Body,
    int DeliveryAttempt,
    object DeliveryHandle);

public interface IStorageWorkQueue
{
    public Task PublishAsync(StorageQueueName queue, ReadOnlyMemory<byte> body, CancellationToken cancellationToken);

    public IAsyncEnumerable<StorageQueueMessage> ReceiveAsync(
        StorageQueueName queue,
        CancellationToken cancellationToken);

    public Task CompleteAsync(StorageQueueName queue, StorageQueueMessage message, CancellationToken cancellationToken);

    public Task AbandonAsync(StorageQueueName queue, StorageQueueMessage message, CancellationToken cancellationToken);
}
