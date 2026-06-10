using System.Text.Json;
using PrintAgent.Printing;

namespace PrintAgent.Protocol.Handlers;

public sealed class PrintHandler : IRpcHandler
{
    public string Method => "print";
    public bool RequiresPairedConnection => true;

    private readonly IPrintJobSubmitter _jobs;
    private readonly IPrinterService _printers;
    private readonly int _maxBytes;

    public PrintHandler(IPrintJobSubmitter jobs, IPrinterService printers, int maxBytes)
    {
        _jobs = jobs;
        _printers = printers;
        _maxBytes = maxBytes;
    }

    public async Task<object?> HandleAsync(JsonElement? @params, ConnectionContext connection, CancellationToken ct)
    {
        if (@params is null) throw new ArgumentException("Missing params.");
        var p = @params.Value;

        if (!p.TryGetProperty("printerName", out var printerEl) || printerEl.ValueKind != JsonValueKind.String)
            throw new ArgumentException("Missing or invalid 'printerName'.");

        if (!p.TryGetProperty("pdfBase64", out var pdfEl) || pdfEl.ValueKind != JsonValueKind.String)
            throw new ArgumentException("Missing or invalid 'pdfBase64'.");

        var printerName = printerEl.GetString()!;

        // Reject argv-poisoning attempts before consulting the printer list.
        if (printerName.Length == 0 || printerName[0] == '-' || printerName[0] == '/')
            throw new RpcApplicationException(JsonRpcErrorCodes.PrinterNotFound,
                $"Printer '{printerName}' is not installed.");

        // Whitelist against the local printer set (defense-in-depth on top of pairing).
        var installed = _printers.List();
        if (!installed.Any(pi => string.Equals(pi.Name, printerName, StringComparison.OrdinalIgnoreCase)))
            throw new RpcApplicationException(JsonRpcErrorCodes.PrinterNotFound,
                $"Printer '{printerName}' is not installed.");

        byte[] pdfBytes;
        try { pdfBytes = Convert.FromBase64String(pdfEl.GetString()!); }
        catch (FormatException) { throw new ArgumentException("PdfDecodeFailed: invalid base64."); }

        if (pdfBytes.Length > _maxBytes)
            throw new ArgumentException($"PDF too large: {pdfBytes.Length} > {_maxBytes}.");

        var options = ParseOptions(p);

        var jobId = await _jobs.SubmitAsync(printerName, pdfBytes, options, connection.ConnectionId, ct);
        return new { jobId = jobId.ToString() };
    }

    private static PrintOptions ParseOptions(JsonElement p)
    {
        if (!p.TryGetProperty("options", out var optsEl)) return new PrintOptions();
        if (optsEl.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Invalid 'options': expected a JSON object.");

        return new PrintOptions(
            Copies: ParseCopies(optsEl),
            PaperSize: ParsePaperSize(optsEl),
            Color: ParseColor(optsEl),
            Orientation: ParseOrientation(optsEl));
    }

    private static int ParseCopies(JsonElement opts)
    {
        if (!opts.TryGetProperty("copies", out var el)) return 1;
        if (el.ValueKind != JsonValueKind.Number || !el.TryGetInt32(out var copies))
            throw new ArgumentException("Invalid 'copies': expected an integer number.");
        return copies;
    }

    private static string? ParsePaperSize(JsonElement opts)
    {
        if (!opts.TryGetProperty("paperSize", out var el)) return null;
        if (el.ValueKind != JsonValueKind.String)
            throw new ArgumentException("Invalid 'paperSize': expected a string.");
        var value = el.GetString();
        if (!IsValidPaperSize(value))
            throw new ArgumentException($"Invalid 'paperSize' value: {value}");
        return value;
    }

    private static bool ParseColor(JsonElement opts)
    {
        if (!opts.TryGetProperty("color", out var el)) return true;
        return el.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new ArgumentException("Invalid 'color': expected a boolean.")
        };
    }

    private static PrintOrientation ParseOrientation(JsonElement opts)
    {
        if (!opts.TryGetProperty("orientation", out var el) || el.ValueKind != JsonValueKind.String)
            return PrintOrientation.Default;
        return el.GetString()?.ToLowerInvariant() switch
        {
            "portrait" => PrintOrientation.Portrait,
            "landscape" => PrintOrientation.Landscape,
            _ => PrintOrientation.Default
        };
    }

    private static bool IsValidPaperSize(string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        if (value.Length > 32) return false;
        if (value[0] == '-') return false;
        foreach (var ch in value)
        {
            var ok = (ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z')
                  || (ch >= '0' && ch <= '9')
                  || ch == ' ' || ch == '_' || ch == '-'
                  || ch == '#' || ch == '(' || ch == ')' || ch == '.';
            if (!ok) return false;
        }
        return true;
    }
}
