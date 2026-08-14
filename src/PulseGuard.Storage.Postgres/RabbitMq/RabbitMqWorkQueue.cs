using Microsoft.Extensions.Options;
using PulseGuard.Storage.Abstractions.Queues;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Threading.Channels;
using System.Runtime.CompilerServices;

namespace PulseGuard.Storage.Postgres.RabbitMq;

internal sealed class RabbitMqWorkQueue(IOptions<RabbitMqOptions> options) : IStorageWorkQueue
{
    private const string ExchangeName = "pulseguard.work";
    private const string DeadLetterExchangeName = "pulseguard.work.dlx";

    public async Task PublishAsync(StorageQueueName queue, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        await using IConnection connection = await CreateConnectionAsync(cancellationToken);
        await using IChannel channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await DeclareTopologyAsync(channel, queue, cancellationToken);

        BasicProperties properties = new()
        {
            Persistent = true,
            MessageId = Guid.NewGuid().ToString("N")
        };

        await channel.BasicPublishAsync(
            ExchangeName,
            GetRoutingKey(queue),
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken);
    }

    public async IAsyncEnumerable<StorageQueueMessage> ReceiveAsync(
        StorageQueueName queue,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using IConnection connection = await CreateConnectionAsync(cancellationToken);
        await using IChannel channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await DeclareTopologyAsync(channel, queue, cancellationToken);
        await channel.BasicQosAsync(0, options.Value.PrefetchCount, false, cancellationToken);

        Channel<StorageQueueMessage> messages = Channel.CreateUnbounded<StorageQueueMessage>();
        AsyncEventingBasicConsumer consumer = new(channel);
        consumer.ReceivedAsync += (_, delivery) =>
        {
            StorageQueueMessage message = new(
                delivery.BasicProperties.MessageId ?? Guid.NewGuid().ToString("N"),
                DateTimeOffset.UtcNow,
                delivery.Body,
                GetAttempt(delivery.BasicProperties),
                new RabbitDelivery(channel, delivery.DeliveryTag));

            return messages.Writer.WriteAsync(message, cancellationToken).AsTask();
        };

        string consumerTag = await channel.BasicConsumeAsync(GetQueueName(queue), false, consumer, cancellationToken);

        try
        {
            await foreach (StorageQueueMessage message in messages.Reader.ReadAllAsync(cancellationToken))
            {
                yield return message;
            }
        }
        finally
        {
            messages.Writer.TryComplete();
            await channel.BasicCancelAsync(consumerTag, cancellationToken: cancellationToken);
        }
    }

    public Task CompleteAsync(StorageQueueName queue, StorageQueueMessage message, CancellationToken cancellationToken)
        => GetDelivery(message).Channel.BasicAckAsync(GetDelivery(message).DeliveryTag, false, cancellationToken).AsTask();

    public Task AbandonAsync(StorageQueueName queue, StorageQueueMessage message, CancellationToken cancellationToken)
        => GetDelivery(message).Channel.BasicNackAsync(GetDelivery(message).DeliveryTag, false, true, cancellationToken).AsTask();

    private Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken)
    {
        ConnectionFactory factory = new()
        {
            HostName = options.Value.Host,
            Port = options.Value.Port,
            UserName = options.Value.UserName,
            Password = options.Value.Password,
            VirtualHost = options.Value.VirtualHost,
            AutomaticRecoveryEnabled = true
        };

        return factory.CreateConnectionAsync(cancellationToken);
    }

    private static async Task DeclareTopologyAsync(IChannel channel, StorageQueueName queue, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Direct, durable: true, cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(DeadLetterExchangeName, ExchangeType.Direct, durable: true, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(GetQueueName(queue), durable: true, exclusive: false, autoDelete: false, arguments: new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = DeadLetterExchangeName,
            ["x-dead-letter-routing-key"] = GetRoutingKey(queue)
        }, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(GetQueueName(queue), ExchangeName, GetRoutingKey(queue), cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync($"{GetQueueName(queue)}.dlq", durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
        await channel.QueueBindAsync($"{GetQueueName(queue)}.dlq", DeadLetterExchangeName, GetRoutingKey(queue), cancellationToken: cancellationToken);
    }

    private static int GetAttempt(IReadOnlyBasicProperties properties)
        => properties.Headers?.TryGetValue("x-delivery-count", out object? value) == true && value is byte[] bytes && int.TryParse(System.Text.Encoding.UTF8.GetString(bytes), out int attempt)
            ? attempt
            : 1;

    private static RabbitDelivery GetDelivery(StorageQueueMessage message)
        => message.DeliveryHandle as RabbitDelivery
           ?? throw new InvalidOperationException("The queue message was not created by the RabbitMQ provider.");

    private static string GetQueueName(StorageQueueName queue) => queue switch
    {
        StorageQueueName.Pulses => "pulseguard.pulses",
        StorageQueueName.Agents => "pulseguard.agents",
        StorageQueueName.Webhooks => "pulseguard.webhooks",
        _ => throw new ArgumentOutOfRangeException(nameof(queue), queue, "Unsupported storage queue.")
    };

    private static string GetRoutingKey(StorageQueueName queue) => queue switch
    {
        StorageQueueName.Pulses => "pulse",
        StorageQueueName.Agents => "agent",
        StorageQueueName.Webhooks => "webhook",
        _ => throw new ArgumentOutOfRangeException(nameof(queue), queue, "Unsupported storage queue.")
    };

    private sealed record RabbitDelivery(IChannel Channel, ulong DeliveryTag);
}