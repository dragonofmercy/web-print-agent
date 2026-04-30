using System.Text.Json;
using PrintAgent.Printing;

namespace PrintAgent.Protocol.Handlers;

public sealed class PrintHandler : IRpcHandler
{
    public string Method => "print";
    public bool RequiresPairedConnection => true;

    private readonly IPrintJobSubmitter _jobs;
    private readonly int _maxBytes;

    public PrintHandler(IPrintJobSubmitter jobs, int maxBytes)
    {
        _jobs = jobs;
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
        byte[] pdfBytes;
        try { pdfBytes = Convert.FromBase64String(pdfEl.GetString()!); }
        catch (FormatException) { throw new ArgumentException("PdfDecodeFailed: invalid base64."); }

        if (pdfBytes.Length > _maxBytes)
            throw new ArgumentException($"PDF too large: {pdfBytes.Length} > {_maxBytes}.");

        var options = new PrintOptions();
        if (p.TryGetProperty("options", out var optsEl) && optsEl.ValueKind == JsonValueKind.Object)
        {
            var orientation = PrintOrientation.Default;
            if (optsEl.TryGetProperty("orientation", out var or) && or.ValueKind == JsonValueKind.String)
            {
                orientation = or.GetString()?.ToLowerInvariant() switch
                {
                    "portrait" => PrintOrientation.Portrait,
                    "landscape" => PrintOrientation.Landscape,
                    _ => PrintOrientation.Default
                };
            }

            options = new PrintOptions(
                Copies: optsEl.TryGetProperty("copies", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt32() : 1,
                PaperSize: optsEl.TryGetProperty("paperSize", out var ps) && ps.ValueKind == JsonValueKind.String ? ps.GetString() : null,
                Color: optsEl.TryGetProperty("color", out var col) && col.ValueKind == JsonValueKind.False ? false : true,
                Orientation: orientation,
                Tray: optsEl.TryGetProperty("tray", out var tr) && tr.ValueKind == JsonValueKind.String ? tr.GetString() : null);
        }

        var jobId = await _jobs.SubmitAsync(printerName, pdfBytes, options, connection.ConnectionId, ct);
        return new { jobId = jobId.ToString() };
    }
}
