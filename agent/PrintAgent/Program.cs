using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using PrintAgent.Hosting;
using PrintAgent.Logging;
using PrintAgent.Printing;
using PrintAgent.Protocol;
using PrintAgent.Protocol.Events;
using PrintAgent.Protocol.Handlers;
using PrintAgent.Security;
using PrintAgent.Storage;
using PrintAgent.Tray;
using Serilog;
using Velopack;

namespace PrintAgent;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        VelopackApp.Build()
            .OnFirstRun(_ => CreateStartupShortcut(Environment.ProcessPath ?? AppContext.BaseDirectory))
            .OnBeforeUninstallFastCallback(_ =>
            {
                KillRunningInstance();
                RemoveStartupShortcut();
                CertificateService.TryUninstallFromTrustedRoot();
            })
            .Run();

        using var mutex = new Mutex(initiallyOwned: true, "Global\\PrintAgent.SingleInstance", out var isOwner);
        if (!isOwner) return;

        ApplicationConfiguration.Initialize();

        var paths = new Paths();
        paths.EnsureLayout();

        var options = LoadOptions();

        var logger = LoggerSetup.Create(paths.LogsDirectory, options.LogLevel);
        Log.Logger = logger;
        logger.Information("PrintAgent {Version} starting", Assembly.GetExecutingAssembly().GetName().Version);

        try
        {
            CleanupOrphanTempPdfs(paths);
            var cert = CertificateService.EnsureCertificate(paths.PfxFile, paths.PfxPasswordFile);
            CertificateService.TryInstallToTrustedRoot(cert);

            ExtractEmbeddedSumatraPdf(paths.SumatraPdfPath);

            var configStore = new ConfigStore(paths.ConfigFile);
            var origins = new OriginAuthorizationService(configStore, options.AllowInsecureOrigins);

            int? boundPortRef = null;
            var trayHost = new TrayIconHost(paths, getBoundPort: () => boundPortRef, onQuit: () => Application.Exit());
            trayHost.Show();
            var pairingUi = new WinFormsPairingUi(trayHost.UiAnchor);
            var pairing = new PairingService(configStore, pairingUi,
                refusalCooldown: options.PairingRefusalCooldown, timeout: options.PairingPromptTimeout);

            var publisher = new JobEventPublisher();
            var runner = new SumatraPdfRunner(paths.SumatraPdfPath);
            var jobs = new PrintJobService(publisher, runner,
                tempDirectory: Path.GetTempPath(),
                maxJobsPerConnection: options.MaxJobsPerConnection);

            var printerService = new PrinterService();

            var router = new RpcRouter(new IRpcHandler[]
            {
                new AgentHelloHandler(pairing),
                new GetLocalPrintersHandler(printerService),
                new PrintHandler(jobs, options.MaxMessageBytes),
                new GetJobStatusHandler(jobs),
            });

            var endpoint = new WebSocketEndpoint(router, origins, publisher, logger, options.MaxMessageBytes);

            var host = new KestrelHost();
            host.StartAsync(options.PortRange, cert, endpoint, logger, CancellationToken.None).GetAwaiter().GetResult();
            boundPortRef = host.BoundPort;
            logger.Information("PrintAgent listening on wss://127.0.0.1:{Port}", host.BoundPort);
            configStore.SetLastBoundPort(host.BoundPort!.Value);

            Application.Run();

            host.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
            jobs.Dispose();
            trayHost.Dispose();
        }
        catch (Exception ex)
        {
            logger.Fatal(ex, "PrintAgent crashed during bootstrap.");
            MessageBox.Show($"PrintAgent failed to start:\n{ex.Message}", "PrintAgent",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static PrintAgentOptions LoadOptions()
    {
        var json = File.Exists("appsettings.json") ? File.ReadAllText("appsettings.json") : "{}";
        var doc = System.Text.Json.JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty(PrintAgentOptions.SectionName, out var section))
            return new PrintAgentOptions();

        int[] portRange = section.TryGetProperty("PortRange", out var pr) && pr.ValueKind == System.Text.Json.JsonValueKind.Array
            ? pr.EnumerateArray().Select(e => e.GetInt32()).ToArray()
            : new[] { 8443, 8444, 8445, 8446, 8447 };

        return new PrintAgentOptions
        {
            PortRange = portRange,
            MaxMessageBytes = section.TryGetProperty("MaxMessageBytes", out var mb) ? mb.GetInt32() : 20 * 1024 * 1024,
            MaxJobsPerConnection = section.TryGetProperty("MaxJobsPerConnection", out var mj) ? mj.GetInt32() : 5,
            PairingPromptTimeout = TimeSpan.FromSeconds(section.TryGetProperty("PairingPromptTimeoutSeconds", out var pt) ? pt.GetInt32() : 60),
            PairingRefusalCooldown = TimeSpan.FromMinutes(section.TryGetProperty("PairingRefusalCooldownMinutes", out var rc) ? rc.GetInt32() : 5),
            AllowInsecureOrigins = section.TryGetProperty("AllowInsecureOrigins", out var aio) && aio.GetBoolean(),
            LogLevel = section.TryGetProperty("LogLevel", out var ll) ? ll.GetString() ?? "Information" : "Information",
        };
    }

    private static void CleanupOrphanTempPdfs(Paths paths)
    {
        try
        {
            var tmp = Path.GetTempPath();
            foreach (var file in Directory.EnumerateFiles(tmp, "printagent-*.pdf"))
            {
                if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddHours(-24))
                    File.Delete(file);
            }
        }
        catch { /* best effort */ }
    }

    private static void ExtractEmbeddedSumatraPdf(string targetPath)
    {
        var sourcePath = Path.Combine(AppContext.BaseDirectory, "Resources", "SumatraPDF.exe");
        if (File.Exists(sourcePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            if (!File.Exists(targetPath) || !FileHashesMatch(sourcePath, targetPath))
                File.Copy(sourcePath, targetPath, overwrite: true);
        }
    }

    private static bool FileHashesMatch(string a, string b)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        using var fa = File.OpenRead(a);
        using var fb = File.OpenRead(b);
        var ha = sha.ComputeHash(fa);
        var hb = sha.ComputeHash(fb);
        return ha.SequenceEqual(hb);
    }

    private static void CreateStartupShortcut(string? targetExe)
    {
        if (string.IsNullOrEmpty(targetExe)) return;
        try
        {
            var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            var shortcutPath = Path.Combine(startupFolder, "PrintAgent.lnk");
            if (File.Exists(shortcutPath)) return;
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null) return;
            dynamic shell = Activator.CreateInstance(shellType)!;
            var shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetExe;
            shortcut.WorkingDirectory = Path.GetDirectoryName(targetExe);
            shortcut.Description = "PrintAgent";
            shortcut.Save();
        }
        catch { /* best effort */ }
    }

    private static void RemoveStartupShortcut()
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "PrintAgent.lnk");
            if (File.Exists(path)) File.Delete(path);
        }
        catch { /* best effort */ }
    }

    private static void KillRunningInstance()
    {
        try
        {
            foreach (var process in System.Diagnostics.Process.GetProcessesByName("PrintAgent"))
            {
                if (process.Id != System.Diagnostics.Process.GetCurrentProcess().Id)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(3000);
                }
            }
        }
        catch { /* best effort */ }
    }
}
