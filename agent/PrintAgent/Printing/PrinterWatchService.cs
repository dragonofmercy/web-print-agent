using System.Management;
using System.Runtime.Versioning;
using PrintAgent.Hosting;
using Serilog;

namespace PrintAgent.Printing;

/// <summary>
/// Watches Win32_Printer instance operation events via WMI and raises a coalesced
/// <see cref="Changed"/> event when printers are added/removed/modified. WMI access is
/// isolated here (this is the only OS-touching class for printer hot-plug); the broadcast
/// and coalescing concerns live in OS-free, unit-testable seams (ConnectionRegistry, Debouncer).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class PrinterWatchService : IPrinterWatcher
{
    // Spec section 9.5: poll the operation-event stream every 2 seconds for Win32_Printer changes.
    private const string Wql = "SELECT * FROM __InstanceOperationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_Printer'";

    private readonly ILogger _log;
    private readonly ManagementEventWatcher _watcher;
    private readonly Debouncer _debouncer;
    private readonly object _gate = new();
    private bool _disposed;

    public event EventHandler? Changed;

    public PrinterWatchService(ILogger logger, TimeSpan? debounceInterval = null)
    {
        _log = logger;
        _debouncer = new Debouncer(debounceInterval ?? TimeSpan.FromMilliseconds(750), RaiseChanged);
        _watcher = new ManagementEventWatcher(new EventQuery(Wql));
        // A burst of WMI operation events (Create/Modify/Delete, plus the WITHIN 2 batching)
        // collapses into a single Changed via the debouncer.
        _watcher.EventArrived += (_, _) => _debouncer.Trigger();
    }

    public void Start()
    {
        // Let exceptions propagate; Program.cs wraps Start in try/catch+log so a WMI-unavailable
        // box does not crash bootstrap (mirrors the defensive updater wiring).
        _watcher.Start();
        _log.Information("Printer hot-plug watcher started.");
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }

        try { _watcher.Stop(); }
        catch { /* best effort: the query may never have started */ }
        try { _watcher.Dispose(); }
        catch { /* best effort */ }
        _debouncer.Dispose();
    }
}
