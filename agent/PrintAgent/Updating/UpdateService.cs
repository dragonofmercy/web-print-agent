using Serilog;

namespace PrintAgent.Updating;

/// <summary>
/// Headless auto-update orchestrator. Decides when to check, stage, notify and apply updates.
/// Holds no Velopack or WinForms reference - those live behind IUpdateClient and IUpdateUi.
/// </summary>
public sealed class UpdateService : IDisposable
{
    private readonly IUpdateClient _client;
    private readonly IUpdateUi _ui;
    private readonly Func<bool> _hasActiveJobs;
    private readonly bool _enabled;
    private readonly TimeSpan _initialDelay;
    private readonly TimeSpan _interval;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _checkLock = new(1, 1);

    private AgentUpdate? _staged;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public UpdateService(IUpdateClient client, IUpdateUi ui, Func<bool> hasActiveJobs,
        bool enabled, TimeSpan initialDelay, TimeSpan interval, ILogger logger)
    {
        _client = client;
        _ui = ui;
        _hasActiveJobs = hasActiveJobs;
        _enabled = enabled;
        _initialDelay = initialDelay;
        _interval = interval;
        _logger = logger;
    }

    /// <summary>Applies any update staged in a previous session (silently if idle), then starts the periodic loop.</summary>
    public Task StartAsync(CancellationToken ct)
    {
        if (!_enabled)
        {
            _logger.Information("Auto-update disabled by configuration.");
            return Task.CompletedTask;
        }

        if (!_client.IsInstalled)
        {
            _logger.Debug("Not an installed build; auto-update inactive.");
            return Task.CompletedTask;
        }

        if (_client.PendingUpdate is { } pending)
        {
            _staged = pending;
            // Silent at boot: apply if idle; if a job is running, keep it staged for the next cycle/boot.
            if (TryApplyNow(pending)) return Task.CompletedTask; // process is restarting
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _loopTask = RunLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(_initialDelay, ct);
            while (!ct.IsCancellationRequested)
            {
                await CheckNowAsync(manual: false);
                await Task.Delay(_interval, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    /// <summary>Checks the feed; on a hit, downloads and notifies. Single-flight: a concurrent check is skipped.</summary>
    public async Task CheckNowAsync(bool manual)
    {
        if (!_enabled || !_client.IsInstalled) return;
        if (!await _checkLock.WaitAsync(0)) return;

        try
        {
            var token = _cts?.Token ?? CancellationToken.None;

            AgentUpdate? update;
            try
            {
                update = await _client.CheckAsync(token);
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Update check failed.");
                return;
            }

            if (update is null)
            {
                if (manual) _ui.NotifyUpToDate();
                return;
            }

            try
            {
                await _client.DownloadAsync(update, token);
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Update download failed.");
                return;
            }

            _staged = update;
            _logger.Information("Update {Version} staged and ready.", update.Version);
            _ui.NotifyUpdateReady(update.Version);
        }
        finally
        {
            _checkLock.Release();
        }
    }

    /// <summary>Invoked when the user clicks the toast. Applies if idle, otherwise tells the user it will apply later.</summary>
    public void OnUserWantsRestart()
    {
        if (_staged is not { } staged) return;
        if (!TryApplyNow(staged)) _ui.NotifyBusyDeferred();
    }

    /// <summary>Applies and restarts if no print job is active. Returns false (no-op) when busy.</summary>
    private bool TryApplyNow(AgentUpdate update)
    {
        if (_hasActiveJobs())
        {
            _logger.Information("Update {Version} deferred: a print job is active.", update.Version);
            return false;
        }

        _logger.Information("Applying update {Version} and restarting.", update.Version);
        _client.ApplyAndRestart(update);
        return true;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        // Let any in-flight check observe cancellation and release the semaphore
        // before we dispose it, otherwise its finally-block Release() would throw
        // ObjectDisposedException on a background task during shutdown.
        try { _loopTask?.Wait(TimeSpan.FromSeconds(2)); }
        catch (AggregateException) { /* cancellation/teardown */ }
        _cts?.Dispose();
        _checkLock.Dispose();
    }
}
