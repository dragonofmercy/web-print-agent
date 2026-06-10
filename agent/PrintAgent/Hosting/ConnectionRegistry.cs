using System.Collections.Concurrent;
using PrintAgent.Protocol;

namespace PrintAgent.Hosting;

/// <summary>
/// Tracks live WebSocket connections and broadcasts notifications to all paired ones.
/// This is the OS-free, unit-testable seam for fan-out notifications such as
/// <c>printers.changed</c>. It does not own sockets; it only references the
/// per-connection <see cref="ConnectionContext.SendNotificationAsync"/> delegate,
/// which is already concurrency-safe per connection (routes through the SendGate).
/// </summary>
public sealed class ConnectionRegistry
{
    private readonly ConcurrentDictionary<Guid, ConnectionContext> _connections = new();

    public int Count => _connections.Count;

    public void Add(ConnectionContext connection) => _connections[connection.ConnectionId] = connection;

    public void Remove(Guid connectionId) => _connections.TryRemove(connectionId, out _);

    /// <summary>
    /// Sends the notification to every currently-paired connection concurrently.
    /// A single dead or failing connection never aborts delivery to the others:
    /// each send is wrapped so its exception is swallowed (a dead socket is expected).
    /// Completes immediately when there are no paired connections.
    /// </summary>
    public Task BroadcastToPairedAsync(JsonRpcNotification notification, CancellationToken ct)
    {
        var targets = _connections.Values.Where(c => c.IsPaired).ToArray();
        if (targets.Length == 0) return Task.CompletedTask;

        var sends = new Task[targets.Length];
        for (var i = 0; i < targets.Length; i++)
        {
            var target = targets[i];
            sends[i] = SendSafelyAsync(target, notification, ct);
        }

        return Task.WhenAll(sends);
    }

    private static async Task SendSafelyAsync(ConnectionContext connection, JsonRpcNotification notification, CancellationToken ct)
    {
        try
        {
            await connection.SendNotificationAsync(notification, ct);
        }
        catch
        {
            // A dead/closing socket throwing here is expected; never let it fault the whole broadcast.
        }
    }
}
