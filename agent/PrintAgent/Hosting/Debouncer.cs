using System.Threading;

namespace PrintAgent.Hosting;

/// <summary>
/// Coalesces a burst of <see cref="Trigger"/> calls into a single callback invocation
/// that fires once after <c>interval</c> of quiet. Each Trigger restarts the window, so
/// rapid repeated triggers collapse to one callback. Thread-safe and disposable: after
/// <see cref="Dispose"/>, a pending callback that has not yet started will not fire and
/// further triggers are no-ops (a callback already in flight when Dispose runs may still
/// complete - the caller's callback must tolerate that).
/// </summary>
public sealed class Debouncer : IDisposable
{
    private readonly TimeSpan _interval;
    private readonly Action _callback;
    private readonly object _gate = new();
    private readonly System.Threading.Timer _timer;
    private bool _disposed;

    public Debouncer(TimeSpan interval, Action callback)
    {
        _interval = interval;
        _callback = callback ?? throw new ArgumentNullException(nameof(callback));
        // Created idle; Trigger() arms it. The timer callback re-checks _disposed under the lock.
        _timer = new System.Threading.Timer(OnTimer, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public void Trigger()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _timer.Change(_interval, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnTimer(object? state)
    {
        lock (_gate)
        {
            if (_disposed) return;
        }

        _callback();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _timer.Dispose();
        }
    }
}
