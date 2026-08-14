using PulseGuard.Entities;
using PulseGuard.Models;
using PulseGuard.Storage.Abstractions.Queues;
using System.Runtime.CompilerServices;

namespace PulseGuard.Services;

public readonly record struct WebhookEventMessage(StorageQueueMessage Message, WebhookEventBase? WebhookEvent)
{
    public string Id => Message.MessageId;
}

public sealed class WebhookService(IStorageWorkQueue queue, ILogger<WebhookService> logger)
{
    private readonly IStorageWorkQueue _queue = queue;
    private readonly ILogger<WebhookService> _logger = logger;

    public async IAsyncEnumerable<WebhookEventMessage> ReceiveMessagesAsync([EnumeratorCancellation] CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await foreach (StorageQueueMessage message in _queue.ReceiveAsync(StorageQueueName.Webhooks, token))
            {
                WebhookEventBase? webhookEvent;

                try
                {
                    webhookEvent = PulseSerializerContext.Default.WebhookEventBase.Deserialize(new BinaryData(message.Body));
                }
                catch (Exception ex)
                {
                    _logger.FailedToDeserializeWebhookEvent(ex, message.MessageId, new BinaryData(message.Body).ToString());
                    webhookEvent = null;
                }

                yield return new(message, webhookEvent);
            }
        }
    }

    public async Task<bool> DeleteMessageAsync(WebhookEventMessage message)
    {
        await _queue.CompleteAsync(StorageQueueName.Webhooks, message.Message, CancellationToken.None);
        return true;
    }

    public Task PostAsync(Pulse old, Pulse @new, PulseConfiguration options, CancellationToken token)
    {
        double? duration = (old.LastUpdatedTimestamp - old.CreationTimestamp).TotalMinutes;

        WebhookEvent webhookEvent = new
        (
            @new.Sqid,
            options.Group,
            options.Name,
            new
            (
                old.State.Stringify(),
                @new.State.Stringify(),
                @new.CreationTimestamp.ToUnixTimeSeconds(),
                duration,
                @new.Message
            )
        );

        return PostAsync(webhookEvent, token);
    }

    public Task PostAsync(Pulse pulse, DateTimeOffset since, int threshold, PulseConfiguration options, CancellationToken token)
    {
        ThresholdWebhookEvent webhookEvent = new(
            pulse.Sqid,
            options.Group,
            options.Name,
            since.ToUnixTimeSeconds(),
            threshold
        );

        return PostAsync(webhookEvent, token);
    }

    private Task PostAsync(WebhookEventBase webhookEvent, CancellationToken token)
    {
        BinaryData data = new(PulseSerializerContext.Default.WebhookEventBase.SerializeToUtf8Bytes(webhookEvent));
        return _queue.PublishAsync(StorageQueueName.Webhooks, data.ToMemory(), token);
    }
}
