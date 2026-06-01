namespace PrintAgent.Updating;

/// <summary>A pending agent update, identified by its version for display. Velopack types never leak past this seam.</summary>
public sealed record AgentUpdate(string Version);

/// <summary>
/// Thin seam over Velopack. The only concrete implementation (VelopackUpdateClient)
/// is the single file that references the Velopack SDK; everything above is unit-tested with a fake.
/// </summary>
public interface IUpdateClient
{
    /// <summary>False for dev/test/run-from-bin builds (Velopack is not installed). Updater stays inactive.</summary>
    bool IsInstalled { get; }

    /// <summary>An update already downloaded in a previous session and waiting for restart, or null.</summary>
    AgentUpdate? PendingUpdate { get; }

    /// <summary>Queries the feed. Returns the available update, or null if up to date.</summary>
    Task<AgentUpdate?> CheckAsync(CancellationToken ct);

    /// <summary>Downloads (stages) the given update locally.</summary>
    Task DownloadAsync(AgentUpdate update, CancellationToken ct);

    /// <summary>Applies the staged update and restarts the process into the new version. Does not return.</summary>
    void ApplyAndRestart(AgentUpdate update);
}
