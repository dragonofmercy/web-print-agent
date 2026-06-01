using Velopack;
using Velopack.Sources;

namespace PrintAgent.Updating;

/// <summary>
/// The ONLY file in PrintAgent that references the Velopack SDK.
/// Maps Velopack's <see cref="UpdateManager"/> to the <see cref="IUpdateClient"/> seam so the
/// rest of the application (and all unit tests) never take a direct dependency on Velopack types.
/// This class is intentionally not unit-tested; correctness is validated by the installer smoke test.
/// </summary>
public sealed class VelopackUpdateClient : IUpdateClient
{
    private readonly UpdateManager _manager;

    /// <summary>Cached result of the last successful <see cref="CheckAsync"/> call. Required by DownloadAsync and ApplyAndRestart.</summary>
    private UpdateInfo? _current;

    /// <param name="repoUrl">GitHub repository URL used as the Velopack update source.</param>
    /// <param name="allowPrerelease">When true, pre-release GitHub releases are included in the feed.</param>
    public VelopackUpdateClient(string repoUrl, bool allowPrerelease)
    {
        var source = new GithubSource(repoUrl, accessToken: null, prerelease: allowPrerelease, downloader: null);
        _manager = new UpdateManager(source);
    }

    /// <inheritdoc/>
    public bool IsInstalled => _manager.IsInstalled;

    /// <inheritdoc/>
    /// <remarks>Returns an update already downloaded in a previous session and awaiting restart, or null.</remarks>
    public AgentUpdate? PendingUpdate
    {
        get
        {
            VelopackAsset? pending = _manager.UpdatePendingRestart;
            return pending is null ? null : new AgentUpdate(pending.Version.ToString());
        }
    }

    /// <inheritdoc/>
    public async Task<AgentUpdate?> CheckAsync(CancellationToken ct)
    {
        _current = await _manager.CheckForUpdatesAsync();
        return _current is null ? null : new AgentUpdate(_current.TargetFullRelease.Version.ToString());
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Thrown when called before a successful <see cref="CheckAsync"/>.</exception>
    public async Task DownloadAsync(AgentUpdate update, CancellationToken ct)
    {
        if (_current is null)
            throw new InvalidOperationException("DownloadAsync called before a successful CheckAsync.");

        await _manager.DownloadUpdatesAsync(_current, progress: null, ct);
    }

    /// <inheritdoc/>
    /// <remarks>Does not return in production - the process is restarted into the new version.</remarks>
    public void ApplyAndRestart(AgentUpdate update)
    {
        VelopackAsset? asset = _current?.TargetFullRelease ?? _manager.UpdatePendingRestart;
        if (asset is not null)
            _manager.ApplyUpdatesAndRestart(asset);
    }
}
