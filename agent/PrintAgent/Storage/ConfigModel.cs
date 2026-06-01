namespace PrintAgent.Storage;

public sealed class ConfigModel
{
    public List<string> AllowedOrigins { get; set; } = new();
    public int? LastBoundPort { get; set; }
    public string? CertThumbprint { get; set; }

    /// <summary>Per-machine kill switch for auto-update. Default true; an admin can set false to freeze the version.</summary>
    public bool AutoUpdate { get; set; } = true;
}
