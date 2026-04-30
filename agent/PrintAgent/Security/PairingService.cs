using System.Collections.Concurrent;
using PrintAgent.Storage;

namespace PrintAgent.Security;

public sealed class PairingService
{
    private readonly ConfigStore _configStore;
    private readonly IPairingUi _ui;
    private readonly TimeSpan _refusalCooldown;
    private readonly TimeSpan _promptTimeout;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _refusedUntil = new();

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

        var decision = await _ui.PromptAsync(origin, _promptTimeout, ct);

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
}
