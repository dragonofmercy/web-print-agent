using System.Diagnostics;

namespace PrintAgent.Printing;

public interface ISumatraRunner
{
    Task<SumatraPdfRunner.RunResult> RunAsync(string printerName, string pdfPath, PrintOptions options, CancellationToken ct);
}

public sealed class SumatraPdfRunner : ISumatraRunner
{
    private readonly string _binaryPath;

    public SumatraPdfRunner(string binaryPath) => _binaryPath = binaryPath;

    public static List<string> BuildArguments(string printerName, string pdfPath, PrintOptions options)
    {
        var args = new List<string> { "-print-to", printerName };

        var settings = new List<string>();
        if (options.Copies > 1) settings.Add($"{options.Copies}x");
        if (!string.IsNullOrEmpty(options.PaperSize)) settings.Add($"paper={options.PaperSize}");
        if (!options.Color) settings.Add("monochrome");

        if (settings.Count > 0)
        {
            args.Add("-print-settings");
            args.Add(string.Join(",", settings));
        }

        args.Add("-silent");
        args.Add(pdfPath);
        return args;
    }

    public async Task<RunResult> RunAsync(string printerName, string pdfPath, PrintOptions options, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _binaryPath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in BuildArguments(printerName, pdfPath, options)) psi.ArgumentList.Add(a);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start SumatraPDF process.");

        var stdErr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        return new RunResult(process.ExitCode, stdErr);
    }

    public sealed record RunResult(int ExitCode, string StandardError);
}
