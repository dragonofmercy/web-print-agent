namespace PrintAgent.Storage;

public sealed class ConfigModel
{
    public List<string> AllowedOrigins { get; set; } = new();
    public int? LastBoundPort { get; set; }
    public string? CertThumbprint { get; set; }
}
