using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using PrintAgent.Printing;
using PrintAgent.Protocol;
using PrintAgent.Protocol.Handlers;
using PrintAgent.Tests.Helpers;
using Xunit;

namespace PrintAgent.Tests.Security;

public class PrinterNameValidationTests
{
    private static JsonElement MakeParams(string printerName) => JsonSerializer.SerializeToElement(new
    {
        printerName,
        pdfBase64 = Convert.ToBase64String("%PDF-1.4\n%%EOF"u8.ToArray())
    });

    [Fact]
    public async Task Handle_PrinterNameNotInstalled_ThrowsPrinterNotFound()
    {
        var jobs = Substitute.For<IPrintJobSubmitter>();
        var printers = PrinterServiceFakes.With("HP LaserJet");
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
    public async Task Handle_PrinterNameLooksLikeFlag_ThrowsPrinterNotFoundBeforeConsultingWhitelist(string evil)
    {
        var jobs = Substitute.For<IPrintJobSubmitter>();
        var printers = PrinterServiceFakes.With("HP"); // neutral list; prefix check must fire FIRST
        var handler = new PrintHandler(jobs, printers, maxBytes: 1024);

        var act = () => handler.HandleAsync(MakeParams(evil),
            new ConnectionContext { IsPaired = true }, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<RpcApplicationException>();
        ex.Which.Code.Should().Be(JsonRpcErrorCodes.PrinterNotFound);
        printers.DidNotReceive().Exists(Arg.Any<string>()); // prove the prefix check short-circuited
    }

    [Fact]
    public async Task Handle_NormalPrinterName_ConsultsExistsNotList()
    {
        var jobs = Substitute.For<IPrintJobSubmitter>();
        var printers = PrinterServiceFakes.With("HP LaserJet");
        jobs.SubmitAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<PrintOptions>(),
                Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());
        var handler = new PrintHandler(jobs, printers, maxBytes: 1024);

        await handler.HandleAsync(MakeParams("HP LaserJet"),
            new ConnectionContext { IsPaired = true }, CancellationToken.None);

        // Hot path must use the cheap existence check, not the heavy enumeration.
        printers.Received(1).Exists(Arg.Any<string>());
        printers.DidNotReceive().List();
    }

    [Fact]
    public async Task Handle_PrinterNameInstalledAndSafe_DelegatesToService()
    {
        var jobs = Substitute.For<IPrintJobSubmitter>();
        var printers = PrinterServiceFakes.With("HP LaserJet");
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

    [Fact]
    public async Task Handle_PrinterNameMatchesInstalledIgnoringCase_DelegatesToService()
    {
        var jobs = Substitute.For<IPrintJobSubmitter>();
        var printers = PrinterServiceFakes.With("HP LaserJet");
        jobs.SubmitAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<PrintOptions>(),
                Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());
        var handler = new PrintHandler(jobs, printers, maxBytes: 1024);

        var result = await handler.HandleAsync(MakeParams("hp laserjet"),
            new ConnectionContext { IsPaired = true }, CancellationToken.None);

        result.Should().NotBeNull();
    }
}
