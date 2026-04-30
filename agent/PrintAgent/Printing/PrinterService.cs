using System.Drawing.Printing;
using System.Management;
using System.Runtime.Versioning;

namespace PrintAgent.Printing;

[SupportedOSPlatform("windows")]
public sealed class PrinterService
{
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
        var escaped = printerName.Replace("'", "''");
        var query = new ObjectQuery($"SELECT PrinterStatus FROM Win32_Printer WHERE Name = '{escaped}'");
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
        return "Unknown";
    }
}
