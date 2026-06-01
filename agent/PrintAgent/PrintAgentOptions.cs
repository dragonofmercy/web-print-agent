namespace PrintAgent;

public sealed class PrintAgentOptions
{
    public const string SectionName = "PrintAgent";

    public int[] PortRange { get; init; } = [8443, 8444, 8445, 8446, 8447];
    public int MaxMessageBytes { get; init; } = 20 * 1024 * 1024;
    public int MaxJobsPerConnection { get; init; } = 5;
    public int MaxQueuedJobs { get; init; } = 100;
    public int MaxActiveConnections { get; init; } = 32;
    public int MaxRunSeconds { get; init; } = 60;
    /// <summary>JSON key: "PairingPromptTimeoutSeconds" (integer, seconds).</summary>
    public TimeSpan PairingPromptTimeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>JSON key: "PairingRefusalCooldownMinutes" (integer, minutes).</summary>
    public TimeSpan PairingRefusalCooldown { get; init; } = TimeSpan.FromMinutes(5);
    public bool AllowInsecureOrigins { get; init; } = false;
    public string LogLevel { get; init; } = "Information";

    /// <summary>UI language. "auto" (default) follows OS settings. Otherwise an IETF tag like "en", "fr", "fr-FR".</summary>
    public string Language { get; init; } = "auto";

    /// <summary>GitHub repository URL the updater polls for new releases.</summary>
    public string UpdateRepoUrl { get; init; } = "https://github.com/dragonofmercy/web-print-agent";

    /// <summary>Hours between periodic update checks.</summary>
    public int UpdateCheckIntervalHours { get; init; } = 6;

    /// <summary>When true, prerelease GitHub releases are eligible for auto-update.</summary>
    public bool UpdateAllowPrerelease { get; init; } = false;

    /// <summary>Delay before the first update check after startup, so boot is not slowed.</summary>
    public int UpdateInitialDelaySeconds { get; init; } = 30;
}
