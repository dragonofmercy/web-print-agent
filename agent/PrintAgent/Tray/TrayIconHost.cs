using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using PrintAgent.Localization;
using PrintAgent.Storage;

namespace PrintAgent.Tray;

public sealed class TrayIconHost : IDisposable
{
    private readonly NotifyIcon _icon = new();
    private readonly ContextMenuStrip _menu = new();
    private readonly Paths _paths;
    private readonly Func<int?> _getBoundPort;
    private readonly Action _onQuit;

    public Control UiAnchor { get; } = new Control();

    public TrayIconHost(Paths paths, Func<int?> getBoundPort, Action onQuit)
    {
        _paths = paths;
        _getBoundPort = getBoundPort;
        _onQuit = onQuit;
        _ = UiAnchor.Handle; // force handle creation on UI thread
    }

    public void Show()
    {
        _icon.Icon = LoadEmbeddedIcon() ?? SystemIcons.Application;
        _icon.Visible = true;
        _icon.Text = "PrintAgent";

        var statusItem = new ToolStripMenuItem(Strings.TrayStatus);
        statusItem.Click += (_, _) =>
        {
            var port = _getBoundPort();
            MessageBox.Show(
                port.HasValue ? Strings.TrayStatusRunning(port.Value) : Strings.TrayStatusStopped,
                "PrintAgent", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        var logsItem = new ToolStripMenuItem(Strings.TrayOpenLogs);
        logsItem.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(_paths.LogsDirectory) { UseShellExecute = true }); }
            catch { /* ignore */ }
        };

        var quitItem = new ToolStripMenuItem(Strings.TrayQuit);
        quitItem.Click += (_, _) => _onQuit();

        _menu.Items.Add(statusItem);
        _menu.Items.Add(logsItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(quitItem);

        _icon.ContextMenuStrip = _menu;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
        UiAnchor.Dispose();
    }

    private static Icon? LoadEmbeddedIcon()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("icon.ico");
            if (stream is null) return null;
            return new Icon(stream, SystemInformation.SmallIconSize);
        }
        catch
        {
            return null;
        }
    }
}
