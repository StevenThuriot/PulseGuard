using Microsoft.Extensions.Options;
using PulseGuard.Entities;
using PulseGuard.Models;
using PulseGuard.Services;
using PulseGuard.Storage.Abstractions.Contracts;
using PulseGuard.Storage.Abstractions.Models;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using TableStorage.Linq;

namespace PulseGuard.Routes;

public static class EventRoutes
{
    extension(IEndpointRouteBuilder builder)
    {
        public void MapEvents()
        {
            RouteGroupBuilder group = builder.MapGroup("/api/1.0/pulses/events").WithTags("Events");

            group.MapGet("", (IPulseRegistrationService pulseEventService, IOptions<PulseOptions> options, IServiceConfigurationStore configurations, IHealthHistoryStore history, CancellationToken token)
                                => TypedResults.ServerSentEvents(ListenForNewPulses(options, configurations, history, pulseEventService, null, token)));

            group.MapGet("application/{application}", (IPulseRegistrationService pulseEventService, IOptions<PulseOptions> options, IServiceConfigurationStore configurations, IHealthHistoryStore history, string application, CancellationToken token)
                                => TypedResults.ServerSentEvents(ListenForNewPulses(options, configurations, history, pulseEventService, x => x.Id == application, token)));

            group.MapGet("group/{group}", (IPulseRegistrationService pulseEventService, IOptions<PulseOptions> options, IServiceConfigurationStore configurations, IHealthHistoryStore history, string group, CancellationToken token)
                                            => TypedResults.ServerSentEvents(ListenForNewPulses(options, configurations, history, pulseEventService, x => x.Group == group, token)));
        }
    }

    private static async IAsyncEnumerable<PulseEventInfo> ListenForNewPulses(IOptions<PulseOptions> options, IServiceConfigurationStore configurations, IHealthHistoryStore history, IPulseRegistrationService pulseEventService, Func<PulseEventInfo, bool>? filter, [EnumeratorCancellation] CancellationToken token)
    {
        await foreach (PulseEventInfo existingPulse in await ProcessLastEvents(options, configurations, history, token))
        {
            if (filter is null || filter(existingPulse))
            {
                yield return existingPulse;
            }
        }

        using PulseEventListener listener = filter is null
            ? new PulseEventListener()
            : new FilteredPulseEventListener(filter);

        using IDisposable registration = pulseEventService.Listen(listener);

        await foreach (PulseEventInfo pulseEventInfo in listener.WithCancellation(token))
        {
            yield return pulseEventInfo;
        }
    }

    private static async Task<IAsyncEnumerable<PulseEventInfo>> ProcessLastEvents(IOptions<PulseOptions> options, IServiceConfigurationStore configurations, IHealthHistoryStore history, CancellationToken token)
    {
        DateTimeOffset offset = DateTimeOffset.UtcNow.AddMinutes(-options.Value.Interval * 2.5);
        IReadOnlyDictionary<string, ServiceIdentifierRecord> identifiers = await configurations.GetServiceIdentifiersAsync(token);
        List<PulseEventInfo> result = [];
        foreach (ServiceIdentifierRecord identifier in identifiers.Values)
        {
            PulseStateRecord? current = await history.GetCurrentPulseAsync(identifier.Id, token);
            if (current is not null && current.LastUpdatedTimestamp > offset)
            {
                result.Add(new PulseEventInfo(identifier.Id, identifier.Group ?? string.Empty, identifier.Name, Enum.Parse<PulseStates>(current.State, true), current.LastUpdatedTimestamp, current.LastElapsedMilliseconds ?? 0));
            }
        }

        return Enumerate(result);

        static async IAsyncEnumerable<PulseEventInfo> Enumerate(IEnumerable<PulseEventInfo> items)
        {
            foreach (PulseEventInfo item in items)
            {
                yield return item;
                await Task.Yield();
            }
        }
    }

    private sealed class FilteredPulseEventListener(Func<PulseEventInfo, bool> filter) : PulseEventListener
    {
        private readonly Func<PulseEventInfo, bool> _filter = filter;
        public override void OnPulse(PulseEventInfo pulse)
        {
            if (_filter(pulse))
            {
                base.OnPulse(pulse);
            }
        }
    }

    private class PulseEventListener : IPulseListener, IAsyncEnumerable<PulseEventInfo>, IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new(0);
        private readonly ConcurrentQueue<PulseEventInfo> _queue = [];

        public void Dispose() => _semaphore.Dispose();

        public IAsyncEnumerator<PulseEventInfo> GetAsyncEnumerator(CancellationToken cancellationToken)
            => new PulseEventEnumerator(_queue, _semaphore, cancellationToken);

        public virtual void OnPulse(PulseEventInfo pulse)
        {
            _queue.Enqueue(pulse);
            _semaphore.Release();
        }
    }

    private sealed class PulseEventEnumerator(ConcurrentQueue<PulseEventInfo> queue, SemaphoreSlim semaphore, CancellationToken cancellationToken) : IAsyncEnumerator<PulseEventInfo>
    {
        private readonly ConcurrentQueue<PulseEventInfo> _queue = queue;
        private readonly SemaphoreSlim _semaphore = semaphore;
        private readonly CancellationToken _cancellationToken = cancellationToken;

        public PulseEventInfo Current { get; private set; } = default!;

        public async ValueTask<bool> MoveNextAsync()
        {
            PulseEventInfo? current;

            do
            {
                if (_cancellationToken.IsCancellationRequested)
                {
                    return false;
                }

                await _semaphore.WaitAsync(_cancellationToken);
            }
            while (!_queue.TryDequeue(out current));

            Current = current!;
            return true;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
