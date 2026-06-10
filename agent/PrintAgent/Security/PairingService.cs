using System.Collections.Concurrent;
using PrintAgent.Storage;

namespace PrintAgent.Security;

public interface IPairingCoordinator
{
    Task<PairingDecision> RequestApprovalAsync(string origin, CancellationToken ct);
}

public sealed class PairingService : IPairingCoordinator
{
    private readonly ConfigStore _configStore;
    private readonly IPairingUi _ui;
    private readonly TimeSpan _refusalCooldown;
    private readonly TimeSpan _promptTimeout;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _refusedUntil = new();
    private readonly ConcurrentDictionary<string, Lazy<Task<PairingDecision>>> _pendingPrompts = new();

    public PairingService(ConfigStore configStore, IPairingUi ui, TimeSpan refusalCooldown, TimeSpan timeout)
    {
        _configStore = configStore;
        _ui = ui;
        _refusalCooldown = refusalCooldown;
        _promptTimeout = timeout;
    }

    public async Task<PairingDecision> RequestApprovalAsync(string origin, CancellationToken ct)
    {
        if (_configStore.IsOriginAllowed(origin)) return PairingDecision.Approved;

        if (_refusedUntil.TryGetValue(origin, out var until) && DateTimeOffset.UtcNow < until)
            return PairingDecision.Refused;

        // Coalesce concurrent requests for the same origin onto a single prompt:
        // only the Lazy that wins publication runs PromptAndRecordAsync, every
        // other caller awaits the same pending decision. The shared prompt runs
        // detached from any caller token (its lifetime is bounded by the prompt
        // timeout inside the UI), so one waiter disconnecting cannot resolve the
        // decision for the others; each waiter observes its own token via WaitAsync.
        var pending = _pendingPrompts.GetOrAdd(origin,
            key => new Lazy<Task<PairingDecision>>(() => PromptAndRecordAsync(key)));

        return await pending.Value.WaitAsync(ct);
    }

    private async Task<PairingDecision> PromptAndRecordAsync(string origin)
    {
        try
        {
            var decision = await _ui.PromptAsync(origin, _promptTimeout, CancellationToken.None);

            switch (decision)
            {
                case PairingDecision.Approved:
                    _configStore.AddAllowedOrigin(origin);
                    _refusedUntil.TryRemove(origin, out _);
                    break;
                case PairingDecision.Refused:
                case PairingDecision.TimedOut:
                    _refusedUntil[origin] = DateTimeOffset.UtcNow + _refusalCooldown;
                    break;
            }

            return decision;
        }
        finally
        {
            _pendingPrompts.TryRemove(origin, out _);
        }
    }
}
