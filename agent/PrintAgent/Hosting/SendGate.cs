namespace PrintAgent.Hosting;

/// <summary>
/// Serializes send operations on a single WebSocket connection. WebSocket.SendAsync is not safe for
/// concurrent sends, and both the RPC response path (receive loop) and the job notification path
/// (PrintJobService worker) can write to the same socket from different threads.
/// </summary>
public sealed class SendGate : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task RunAsync(Func<Task> send, CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        try { await send(); }
        finally { _semaphore.Release(); }
    }

    public void Dispose() => _semaphore.Dispose();
}
