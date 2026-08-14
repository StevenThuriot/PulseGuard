using PulseGuard.Models;
using PulseGuard.Storage.Abstractions.Queues;
using System.Runtime.CompilerServices;

namespace PulseGuard.Services;

public readonly record struct PulseEventMessage(StorageQueueMessage Message, DateTimeOffset Created, PulseEvent? PulseEvent)
{
    public string Id => Message.MessageId;
}

public readonly record struct PulseAgentEventMessage(StorageQueueMessage Message, DateTimeOffset Created, AgentReport? AgentReport)
{
    public string Id => Message.MessageId;
}

public sealed class AsyncPulseStoreService(IStorageWorkQueue queue)
{
    private readonly IStorageWorkQueue _queue = queue;

    public async IAsyncEnumerable<PulseEventMessage> ReceiveMessagesAsync([EnumeratorCancellation] CancellationToken token)
    {
        await foreach (StorageQueueMessage message in _queue.ReceiveAsync(StorageQueueName.Pulses, token))
            {
                PulseEvent? pulseEvent = PulseSerializerContext.Default.PulseEvent.Deserialize(new BinaryData(message.Body));
                yield return new(message, message.EnqueuedAt, pulseEvent);
            }
    }

    public async IAsyncEnumerable<PulseAgentEventMessage> ReceiveAgentMessagesAsync([EnumeratorCancellation] CancellationToken token)
    {
        await foreach (StorageQueueMessage message in _queue.ReceiveAsync(StorageQueueName.Agents, token))
            {
                AgentReport? agentEvent = PulseSerializerContext.Default.AgentReport.Deserialize(new BinaryData(message.Body));
                yield return new(message, message.EnqueuedAt, agentEvent);
            }
    }

    public async Task<bool> DeleteMessageAsync(PulseEventMessage message)
    {
        await _queue.CompleteAsync(StorageQueueName.Pulses, message.Message, CancellationToken.None);
        return true;
    }

    public async Task<bool> DeleteMessageAsync(PulseAgentEventMessage message)
    {
        await _queue.CompleteAsync(StorageQueueName.Agents, message.Message, CancellationToken.None);
        return true;
    }

    public Task PostAsync(PulseReport report, long elapsedMilliseconds, CancellationToken token)
    {
        PulseEvent @event = new(elapsedMilliseconds, report);
        BinaryData data = new(PulseSerializerContext.Default.PulseEvent.SerializeToUtf8Bytes(@event));
        return _queue.PublishAsync(StorageQueueName.Pulses, data.ToMemory(), token);
    }

    public async Task PostAsync(IReadOnlyList<AgentReport> reports, CancellationToken token)
    {
        foreach (AgentReport report in reports)
        {
            await PostAsync(report, token);
        }
    }

    public Task PostAsync(AgentReport report, CancellationToken token)
    {
        BinaryData data = new(PulseSerializerContext.Default.AgentReport.SerializeToUtf8Bytes(report));
        return _queue.PublishAsync(StorageQueueName.Agents, data.ToMemory(), token);
    }
}
