using System.Drawing.Printing;
using System.Management;
using System.Runtime.Versioning;

namespace PrintAgent.Printing;

public interface IPrinterService
{
    IReadOnlyList<PrinterInfo> List();

    bool Exists(string name);
}

[SupportedOSPlatform("windows")]
public sealed class PrinterService : IPrinterService
{
    public bool Exists(string name)
    {
        // Cheap membership test over the installed-printer name set only:
        // no WMI status query, no PaperSizes enumeration, no PrinterInfo
        // construction. This is the hot-path "does this name exist?" check.
        foreach (string installed in PrinterSettings.InstalledPrinters)
        {
            if (string.Equals(installed, name, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public IReadOnlyList<PrinterInfo> List()
    {
        var defaults = new PrinterSettings().PrinterName;
        var result = new List<PrinterInfo>();

        foreach (string name in PrinterSettings.InstalledPrinters)
        {
            string status;
            try { status = QueryStatus(name); }
            catch { status = "Unknown"; }

            string[] paperSizes;
            try
            {
                var settings = new PrinterSettings { PrinterName = name };
                paperSizes = settings.PaperSizes
                    .Cast<PaperSize>()
                    .Select(p => p.PaperName)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
            }
            catch { paperSizes = Array.Empty<string>(); }

            result.Add(new PrinterInfo(
                Name: name,
                IsDefault: string.Equals(name, defaults, StringComparison.Ordinal),
                Status: status,
                PaperSizes: paperSizes));
        }

        return result;
    }

    private static string QueryStatus(string printerName)
    {
        // Match by Name AND ShareName because network printers
        // (\\server\printer) appear locally under their own Name OR via the
        // share path; either form may show up depending on how the driver
        // was registered.
        var escaped = printerName.Replace("\\", "\\\\").Replace("'", "''");
        var query = new ObjectQuery(
            $"SELECT PrinterStatus FROM Win32_Printer WHERE Name = '{escaped}' OR ShareName = '{escaped}'");
        using var searcher = new ManagementObjectSearcher(query);
        foreach (ManagementObject mo in searcher.Get())
        {
            var status = Convert.ToInt32(mo["PrinterStatus"]);
            return status switch
            {
                3 => "Idle",
                4 => "Printing",
                5 => "Warmup",
                6 => "Stopped Printing",
                7 => "Offline",
                _ => "Unknown"
            };
        }
        // Network printers managed by another machine's spooler don't show
        // up in the local Win32_Printer set. We listed it via PrinterSettings
        // so it is reachable; report Available rather than Unknown.
        return "Available";
    }
}
