using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using PrintAgent.Printing;
using PrintAgent.Protocol;
using PrintAgent.Protocol.Handlers;
using Xunit;

namespace PrintAgent.Tests.Protocol;

public class PrintHandlerTests
{
    private static IPrinterService PrintersWith(params string[] names)
    {
        var svc = Substitute.For<IPrinterService>();
        svc.List().Returns(names.Select(n => new PrinterInfo(n, false, "Idle", Array.Empty<string>())).ToList());
        return svc;
    }

    [Fact]
    public async Task Handle_ValidParams_DelegatesToServiceAndReturnsJobId()
    {
        var jobs = Substitute.For<IPrintJobSubmitter>();
        var jobId = Guid.NewGuid();
        jobs.SubmitAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<PrintOptions>(),
                Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(jobId);
        var handler = new PrintHandler(jobs, PrintersWith("HP"), maxBytes: 20 * 1024 * 1024);
        var conn = new ConnectionContext { IsPaired = true };
        var pdfBytes = "%PDF-1.4\n%%EOF"u8.ToArray();
        var paramsJson = JsonSerializer.SerializeToElement(new
        {
            printerName = "HP",
            pdfBase64 = Convert.ToBase64String(pdfBytes)
        });

        var result = await handler.HandleAsync(paramsJson, conn, CancellationToken.None);

        var json = JsonSerializer.SerializeToElement(result);
        json.GetProperty("jobId").GetString().Should().Be(jobId.ToString());
    }

    [Fact]
    public async Task Handle_MissingPrinterName_ThrowsArgumentException()
    {
        var handler = new PrintHandler(Substitute.For<IPrintJobSubmitter>(), PrintersWith("HP"), maxBytes: 1024);
        var paramsJson = JsonSerializer.SerializeToElement(new { pdfBase64 = "JVBERi0=" });

        var act = () => handler.HandleAsync(paramsJson, new ConnectionContext { IsPaired = true }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*printerName*");
    }

    [Fact]
    public async Task Handle_MissingPdfBase64_ThrowsArgumentException()
    {
        var handler = new PrintHandler(Substitute.For<IPrintJobSubmitter>(), PrintersWith("HP"), maxBytes: 1024);
        var paramsJson = JsonSerializer.SerializeToElement(new { printerName = "HP" });

        var act = () => handler.HandleAsync(paramsJson, new ConnectionContext { IsPaired = true }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*pdfBase64*");
    }

    [Fact]
    public async Task Handle_PdfTooLarge_ThrowsArgumentException()
    {
        var handler = new PrintHandler(Substitute.For<IPrintJobSubmitter>(), PrintersWith("HP"), maxBytes: 100);
        var bigBytes = new byte[200];
        bigBytes[0] = (byte)'%'; bigBytes[1] = (byte)'P'; bigBytes[2] = (byte)'D'; bigBytes[3] = (byte)'F'; bigBytes[4] = (byte)'-';
        var paramsJson = JsonSerializer.SerializeToElement(new
        {
            printerName = "HP",
            pdfBase64 = Convert.ToBase64String(bigBytes)
        });

        var act = () => handler.HandleAsync(paramsJson, new ConnectionContext { IsPaired = true }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*too large*");
    }
}
