using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using PrintAgent.Printing;
using PrintAgent.Protocol;
using PrintAgent.Protocol.Handlers;
using Xunit;

namespace PrintAgent.Tests.Security;

public class PrinterNameValidationTests
{
    private static IPrinterService PrinterServiceWith(params string[] names)
    {
        var svc = Substitute.For<IPrinterService>();
        svc.List().Returns(names.Select(n => new PrinterInfo(n, false, "Idle", Array.Empty<string>())).ToList());
        return svc;
    }

    private static JsonElement MakeParams(string printerName) => JsonSerializer.SerializeToElement(new
    {
        printerName,
        pdfBase64 = Convert.ToBase64String("%PDF-1.4\n%%EOF"u8.ToArray())
    });

    [Fact]
    public async Task Handle_PrinterNameNotInstalled_ThrowsPrinterNotFound()
    {
        var jobs = Substitute.For<IPrintJobSubmitter>();
        var printers = PrinterServiceWith("HP LaserJet");
        var handler = new PrintHandler(jobs, printers, maxBytes: 1024);

        var act = () => handler.HandleAsync(MakeParams("Bogus Printer"),
            new ConnectionContext { IsPaired = true }, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<RpcApplicationException>();
        ex.Which.Code.Should().Be(JsonRpcErrorCodes.PrinterNotFound);
    }

    [Theory]
    [InlineData("-print-dialog")]
    [InlineData("--print-to-default")]
    [InlineData("/silent")]
    public async Task Handle_PrinterNameLooksLikeFlag_ThrowsPrinterNotFound(string evil)
    {
        var jobs = Substitute.For<IPrintJobSubmitter>();
        var printers = PrinterServiceWith(evil); // even if it WAS installed, we reject it
        var handler = new PrintHandler(jobs, printers, maxBytes: 1024);

        var act = () => handler.HandleAsync(MakeParams(evil),
            new ConnectionContext { IsPaired = true }, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<RpcApplicationException>();
        ex.Which.Code.Should().Be(JsonRpcErrorCodes.PrinterNotFound);
    }

    [Fact]
    public async Task Handle_PrinterNameInstalledAndSafe_DelegatesToService()
    {
        var jobs = Substitute.For<IPrintJobSubmitter>();
        var printers = PrinterServiceWith("HP LaserJet");
        var jobId = Guid.NewGuid();
        jobs.SubmitAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<PrintOptions>(),
                Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(jobId);
        var handler = new PrintHandler(jobs, printers, maxBytes: 1024);

        var result = await handler.HandleAsync(MakeParams("HP LaserJet"),
            new ConnectionContext { IsPaired = true }, CancellationToken.None);

        var json = JsonSerializer.SerializeToElement(result);
        json.GetProperty("jobId").GetString().Should().Be(jobId.ToString());
    }
}
