namespace PrintAgent;

public sealed class PrintAgentOptions
{
    public const string SectionName = "PrintAgent";

    public int[] PortRange { get; init; } = [8443, 8444, 8445, 8446, 8447];
    public int MaxMessageBytes { get; init; } = 20 * 1024 * 1024;
    public int MaxJobsPerConnection { get; init; } = 5;
    /// <summary>JSON key: "PairingPromptTimeoutSeconds" (integer, seconds).</summary>
    public TimeSpan PairingPromptTimeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>JSON key: "PairingRefusalCooldownMinutes" (integer, minutes).</summary>
    public TimeSpan PairingRefusalCooldown { get; init; } = TimeSpan.FromMinutes(5);
    public bool AllowInsecureOrigins { get; init; } = false;
    public string LogLevel { get; init; } = "Information";
}
