using System.Collections.Concurrent;
using PrintAgent.Printing;

namespace PrintAgent.Protocol.Events;

public sealed class JobEventPublisher
{
    private readonly ConcurrentDictionary<Guid, Func<JobEvent, CancellationToken, Task>> _subscribers = new();

    public IDisposable Subscribe(Guid connectionId, Func<JobEvent, CancellationToken, Task> handler)
    {
        _subscribers[connectionId] = handler;
        return new Subscription(this, connectionId, handler);
    }

    public Task PublishAsync(Guid connectionId, JobEvent ev, CancellationToken ct)
    {
        return _subscribers.TryGetValue(connectionId, out var handler)
            ? handler(ev, ct)
            : Task.CompletedTask;
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
