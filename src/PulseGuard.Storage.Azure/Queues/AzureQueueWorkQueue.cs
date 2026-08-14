using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Options;
using PulseGuard.Storage.Abstractions.Queues;
using PulseGuard.Storage.Azure.Configuration;
using System.Runtime.CompilerServices;

namespace PulseGuard.Storage.Azure.Queues;

internal sealed class AzureQueueWorkQueue(IOptions<AzureStorageOptions> options) : IStorageWorkQueue
{
    private readonly IReadOnlyDictionary<StorageQueueName, QueueClient> _queues = Enum.GetValues<StorageQueueName>()
                      .ToDictionary(x => x, x => new QueueClient(options.Value.ConnectionString, GetQueueName(x)));

    public Task PublishAsync(StorageQueueName queue, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
        => GetQueue(queue).SendMessageAsync(BinaryData.FromBytes(body.ToArray()), cancellationToken: cancellationToken);

    public async IAsyncEnumerable<StorageQueueMessage> ReceiveAsync(
        StorageQueueName queue,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        QueueClient client = GetQueue(queue);

        while (!cancellationToken.IsCancellationRequested)
        {
            QueueMessage[] messages = await client.ReceiveMessagesAsync(
                maxMessages: client.MaxPeekableMessages,
                cancellationToken: cancellationToken);

            if (messages.Length is 0)
            {
                yield break;
            }

            foreach (QueueMessage message in messages)
            {
                yield return new(
                    message.MessageId,
                    message.InsertedOn?.ToUniversalTime() ?? DateTimeOffset.UtcNow,
                    message.Body.ToMemory(),
                    checked((int)message.DequeueCount),
                    new AzureQueueDelivery(message.MessageId, message.PopReceipt));
            }
        }
    }

    public Task CompleteAsync(StorageQueueName queue, StorageQueueMessage message, CancellationToken cancellationToken)
    {
        AzureQueueDelivery delivery = GetDelivery(message);
        return GetQueue(queue).DeleteMessageAsync(delivery.MessageId, delivery.PopReceipt, cancellationToken);
    }

    public Task AbandonAsync(StorageQueueName queue, StorageQueueMessage message, CancellationToken cancellationToken)
    {
        // Leaving an Azure Queue message undeleted preserves its visibility-timeout retry behavior.
        return Task.CompletedTask;
    }

    private static AzureQueueDelivery GetDelivery(StorageQueueMessage message)
        => message.DeliveryHandle as AzureQueueDelivery
           ?? throw new InvalidOperationException("The queue message was not created by the Azure provider.");

    private QueueClient GetQueue(StorageQueueName queue)
        => _queues.TryGetValue(queue, out QueueClient? client)
           ? client
           : throw new ArgumentOutOfRangeException(nameof(queue), queue, "Unsupported storage queue.");

    private static string GetQueueName(StorageQueueName queue) => queue switch
    {
        StorageQueueName.Pulses => "pulses",
        StorageQueueName.Agents => "agents",
        StorageQueueName.Webhooks => "webhooks",
        _ => throw new ArgumentOutOfRangeException(nameof(queue), queue, "Unsupported storage queue.")
    };

    private sealed record AzureQueueDelivery(string MessageId, string PopReceipt);
}
