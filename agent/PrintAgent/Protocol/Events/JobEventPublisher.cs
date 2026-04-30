using System.Collections.Concurrent;
using PrintAgent.Printing;

namespace PrintAgent.Protocol.Events;

public sealed class JobEventPublisher
{
    private readonly ConcurrentDictionary<Guid, Func<JobEvent, CancellationToken, Task>> _subscribers = new();

    /// <summary>
    /// Registers a handler for events targeted at the given connection. Subscribing again with the same connectionId overwrites the previous handler.
    /// </summary>
    public IDisposable Subscribe(Guid connectionId, Func<JobEvent, CancellationToken, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _subscribers[connectionId] = handler;
        return new Subscription(this, connectionId, handler);
    }

    /// <summary>
    /// Delivers the event to the connection's subscriber if any. Subscriber exceptions other than OperationCanceledException are swallowed so a faulty subscriber cannot affect the producer.
    /// </summary>
    public async Task PublishAsync(Guid connectionId, JobEvent ev, CancellationToken ct)
    {
        if (!_subscribers.TryGetValue(connectionId, out var handler)) return;
        try { await handler(ev, ct); }
        catch (OperationCanceledException) { throw; }
        catch { /* subscriber failure must not affect the producer */ }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly JobEventPublisher _publisher;
        private readonly Guid _connectionId;
        private readonly Func<JobEvent, CancellationToken, Task> _handler;
        private int _disposed;

        public Subscription(JobEventPublisher publisher, Guid connectionId, Func<JobEvent, CancellationToken, Task> handler)
        {
            _publisher = publisher;
            _connectionId = connectionId;
            _handler = handler;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _publisher._subscribers.TryRemove(
                new KeyValuePair<Guid, Func<JobEvent, CancellationToken, Task>>(_connectionId, _handler));
        }
    }
}
