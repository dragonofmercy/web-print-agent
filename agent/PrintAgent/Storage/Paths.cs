namespace PrintAgent.Storage;

public sealed class Paths
{
    public string AppDataRoot { get; }
    public string ConfigFile => Path.Combine(AppDataRoot, "config.json");
    public string PfxFile => Path.Combine(AppDataRoot, "printagent.pfx");
    public string PfxPasswordFile => Path.Combine(AppDataRoot, "printagent.pfx.password");
    public string LogsDirectory => Path.Combine(AppDataRoot, "logs");
    public string BinDirectory => Path.Combine(AppDataRoot, "bin");
    public string SumatraPdfPath => Path.Combine(BinDirectory, "SumatraPDF.exe");
    public string TempPdfPattern => "printagent-*.pdf";

    public Paths(string? appDataOverride = null)
    {
        AppDataRoot = appDataOverride ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PrintAgent");
    }

    public void EnsureLayout()
    {
        Directory.CreateDirectory(AppDataRoot);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(BinDirectory);
    }
}
