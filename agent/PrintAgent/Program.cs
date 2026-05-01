using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using PrintAgent.Hosting;
using PrintAgent.Localization;
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
                UninstallCertificateAndCleanFiles();
            })
            .Run();

        using var mutex = new Mutex(initiallyOwned: true, "Local\\PrintAgent.SingleInstance", out var isOwner);
        if (!isOwner) return;

        ApplicationConfiguration.Initialize();

        var paths = new Paths();
        paths.EnsureLayout();

        var options = LoadOptions();
        Strings.ApplyCulture(options.Language);

        var logger = LoggerSetup.Create(paths.LogsDirectory, options.LogLevel);
        Log.Logger = logger;
        logger.Information("PrintAgent {Version} starting", Assembly.GetExecutingAssembly().GetName().Version);

        try
        {
            CleanupOrphanTempPdfs(paths);
            var configStore = new ConfigStore(paths.ConfigFile);

            var cert = CertificateService.EnsureCertificate(paths.PfxFile, paths.PfxPasswordFile);

            // If a previously-installed cert exists with a different thumbprint, untrust it
            // before installing the new one. Keeps at most one PrintAgent cert in the store.
            var previousThumbprint = configStore.GetCertThumbprint();
            if (!string.IsNullOrEmpty(previousThumbprint)
                && !string.Equals(previousThumbprint, cert.Thumbprint, StringComparison.OrdinalIgnoreCase))
            {
                CertificateService.TryUninstallFromTrustedRoot(previousThumbprint);
            }

            CertificateService.TryInstallToTrustedRoot(cert);
            configStore.SetCertThumbprint(cert.Thumbprint);

            ExtractEmbeddedSumatraPdf(paths.SumatraPdfPath);

            var origins = new OriginAuthorizationService(configStore, options.AllowInsecureOrigins);

            int? boundPortRef = null;
            var trayHost = new TrayIconHost(paths, configStore, getBoundPort: () => boundPortRef, onQuit: () => Application.Exit());
            trayHost.Show();
            var pairingUi = new WinFormsPairingUi(trayHost.UiAnchor);
            var pairing = new PairingService(configStore, pairingUi,
                refusalCooldown: options.PairingRefusalCooldown, timeout: options.PairingPromptTimeout);

            var publisher = new JobEventPublisher();
            var runner = new SumatraPdfRunner(paths.SumatraPdfPath, options.MaxRunSeconds);
            var jobs = new PrintJobService(publisher, runner,
                tempDirectory: Path.GetTempPath(),
                maxJobsPerConnection: options.MaxJobsPerConnection,
                maxQueuedJobs: options.MaxQueuedJobs);

            var printerService = new PrinterService();

            var router = new RpcRouter(new IRpcHandler[]
            {
                new AgentHelloHandler(pairing),
                new GetLocalPrintersHandler(printerService),
                new PrintHandler(jobs, printerService, options.MaxMessageBytes),
                new GetJobStatusHandler(jobs),
            });

            var endpoint = new WebSocketEndpoint(router, origins, publisher, logger,
                options.MaxMessageBytes, options.MaxActiveConnections);

            var host = new KestrelHost();
            host.StartAsync(options.PortRange, cert, endpoint, logger, CancellationToken.None).GetAwaiter().GetResult();
            boundPortRef = host.BoundPort;
            logger.Information("PrintAgent listening on wss://127.0.0.1:{Port}", host.BoundPort);
            configStore.SetLastBoundPort(host.BoundPort!.Value);

            Application.Run();

            logger.Information("PrintAgent shutting down...");

            // Hide the tray icon first so the user gets immediate visual feedback.
            trayHost.Dispose();

            // Close active WebSockets proactively, then stop Kestrel with a short timeout.
            // Without this, Kestrel waits for in-flight WebSockets (which auto-reconnect-loop
            // clients would keep alive) for up to 30s.
            using (var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
            {
                try
                {
                    endpoint.CloseAllAsync(stopCts.Token).GetAwaiter().GetResult();
                    host.StopAsync(stopCts.Token).GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    logger.Warning("Shutdown did not complete within 3s; forcing exit.");
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, "Error during shutdown.");
                }
            }

            jobs.Dispose();
            logger.Information("PrintAgent stopped.");
        }
        catch (Exception ex)
        {
            logger.Fatal(ex, "PrintAgent crashed during bootstrap.");
            MessageBox.Show(Strings.BootstrapFailedToStart(ex.Message), Strings.AppName,
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
            MaxQueuedJobs = section.TryGetProperty("MaxQueuedJobs", out var mqj) ? mqj.GetInt32() : 100,
            MaxActiveConnections = section.TryGetProperty("MaxActiveConnections", out var mac) ? mac.GetInt32() : 32,
            MaxRunSeconds = section.TryGetProperty("MaxRunSeconds", out var mrs) ? mrs.GetInt32() : 60,
            PairingPromptTimeout = TimeSpan.FromSeconds(section.TryGetProperty("PairingPromptTimeoutSeconds", out var pt) ? pt.GetInt32() : 60),
            PairingRefusalCooldown = TimeSpan.FromMinutes(section.TryGetProperty("PairingRefusalCooldownMinutes", out var rc) ? rc.GetInt32() : 5),
            AllowInsecureOrigins = section.TryGetProperty("AllowInsecureOrigins", out var aio) && aio.GetBoolean(),
            LogLevel = section.TryGetProperty("LogLevel", out var ll) ? ll.GetString() ?? "Information" : "Information",
            Language = section.TryGetProperty("Language", out var lang) ? lang.GetString() ?? "auto" : "auto",
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
        if (!SumatraExtraction.TryExtract(targetPath, out var warning))
        {
            if (warning is not null) Log.Logger.Warning(warning);
            return;
        }
        Log.Logger.Information("SumatraPDF.exe verified at {Path}.", targetPath);
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
            shortcut.Description = Strings.AppName;
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

    private static void UninstallCertificateAndCleanFiles()
    {
        try
        {
            var paths = new Paths();
            if (File.Exists(paths.ConfigFile))
            {
                var thumbprint = new ConfigStore(paths.ConfigFile).GetCertThumbprint();
                if (!string.IsNullOrEmpty(thumbprint))
                    CertificateService.TryUninstallFromTrustedRoot(thumbprint);
            }

            // Best-effort: delete PFX and DPAPI password file so re-install starts fresh.
            try { if (File.Exists(paths.PfxFile)) File.Delete(paths.PfxFile); } catch { }
            try { if (File.Exists(paths.PfxPasswordFile)) File.Delete(paths.PfxPasswordFile); } catch { }
        }
        catch { /* best effort */ }
    }
}
