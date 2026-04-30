using System.Text.Json;
using PrintAgent.Printing;

namespace PrintAgent.Protocol.Handlers;

public sealed class GetLocalPrintersHandler : IRpcHandler
{
    public string Method => "getLocalPrinters";
    public bool RequiresPairedConnection => true;

    private readonly IPrinterService _printerService;

    public GetLocalPrintersHandler(IPrinterService printerService) => _printerService = printerService;

    public Task<object?> HandleAsync(JsonElement? @params, ConnectionContext connection, CancellationToken ct)
    {
        var list = _printerService.List().Select(p => new
        {
            name = p.Name,
            isDefault = p.IsDefault,
            status = p.Status,
            paperSizes = p.PaperSizes
        });
        return Task.FromResult<object?>(list);
    }
}
