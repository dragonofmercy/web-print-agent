using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using PrintAgent.Printing;
using PrintAgent.Protocol;
using PrintAgent.Protocol.Handlers;
using PrintAgent.Tests.Helpers;
using Xunit;

namespace PrintAgent.Tests.Protocol;

public class PrintHandlerTests
{
    [Fact]
    public async Task Handle_ValidParams_DelegatesToServiceAndReturnsJobId()
    {
        var jobs = Substitute.For<IPrintJobSubmitter>();
        var jobId = Guid.NewGuid();
        jobs.SubmitAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<PrintOptions>(),
                Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(jobId);
        var handler = new PrintHandler(jobs, PrinterServiceFakes.With("HP"), maxBytes: 20 * 1024 * 1024);
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
        var handler = new PrintHandler(Substitute.For<IPrintJobSubmitter>(), PrinterServiceFakes.With("HP"), maxBytes: 1024);
        var paramsJson = JsonSerializer.SerializeToElement(new { pdfBase64 = "JVBERi0=" });

        var act = () => handler.HandleAsync(paramsJson, new ConnectionContext { IsPaired = true }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*printerName*");
    }

    [Fact]
    public async Task Handle_MissingPdfBase64_ThrowsArgumentException()
    {
        var handler = new PrintHandler(Substitute.For<IPrintJobSubmitter>(), PrinterServiceFakes.With("HP"), maxBytes: 1024);
        var paramsJson = JsonSerializer.SerializeToElement(new { printerName = "HP" });

        var act = () => handler.HandleAsync(paramsJson, new ConnectionContext { IsPaired = true }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*pdfBase64*");
    }

    private static (PrintHandler Handler, IPrintJobSubmitter Jobs) MakeHandler()
    {
        var jobs = Substitute.For<IPrintJobSubmitter>();
        jobs.SubmitAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<PrintOptions>(),
                Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());
        return (new PrintHandler(jobs, PrinterServiceFakes.With("HP"), maxBytes: 1024), jobs);
    }

    private static JsonElement ParamsWithOptions(object? options) => JsonSerializer.SerializeToElement(new
    {
        printerName = "HP",
        pdfBase64 = Convert.ToBase64String("%PDF-1.4\n%%EOF"u8.ToArray()),
        options
    });

    private static JsonElement ParamsWithRawOptions(string optionsJson)
    {
        var pdf = Convert.ToBase64String("%PDF-1.4\n%%EOF"u8.ToArray());
        return JsonDocument.Parse("{\"printerName\":\"HP\",\"pdfBase64\":\"" + pdf + "\",\"options\":" + optionsJson + "}").RootElement;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_ColorBoolean_PassesValueThrough(bool color)
    {
        var (handler, jobs) = MakeHandler();

        await handler.HandleAsync(ParamsWithOptions(new { color }),
            new ConnectionContext { IsPaired = true }, CancellationToken.None);

        await jobs.Received(1).SubmitAsync("HP", Arg.Any<byte[]>(),
            Arg.Is<PrintOptions>(o => o.Color == color), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ColorAbsent_DefaultsToTrue()
    {
        var (handler, jobs) = MakeHandler();

        await handler.HandleAsync(ParamsWithOptions(new { copies = 1 }),
            new ConnectionContext { IsPaired = true }, CancellationToken.None);

        await jobs.Received(1).SubmitAsync("HP", Arg.Any<byte[]>(),
            Arg.Is<PrintOptions>(o => o.Color == true), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"false\"")]
    [InlineData("0")]
    [InlineData("{}")]
    [InlineData("[]")]
    public async Task Handle_ColorWrongType_ThrowsArgumentException(string colorJson)
    {
        var (handler, _) = MakeHandler();
        var paramsJson = ParamsWithRawOptions("{\"color\":" + colorJson + "}");

        var act = () => handler.HandleAsync(paramsJson, new ConnectionContext { IsPaired = true }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*color*");
    }

    [Fact]
    public async Task Handle_CopiesAbsent_DefaultsToOne()
    {
        var (handler, jobs) = MakeHandler();

        await handler.HandleAsync(ParamsWithOptions(new { color = true }),
            new ConnectionContext { IsPaired = true }, CancellationToken.None);

        await jobs.Received(1).SubmitAsync("HP", Arg.Any<byte[]>(),
            Arg.Is<PrintOptions>(o => o.Copies == 1), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CopiesNumber_PassesValueThrough()
    {
        var (handler, jobs) = MakeHandler();

        await handler.HandleAsync(ParamsWithOptions(new { copies = 3 }),
            new ConnectionContext { IsPaired = true }, CancellationToken.None);

        await jobs.Received(1).SubmitAsync("HP", Arg.Any<byte[]>(),
            Arg.Is<PrintOptions>(o => o.Copies == 3), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("\"2\"")]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("2.5")]
    public async Task Handle_CopiesWrongTypeOrNonIntegral_ThrowsArgumentException(string copiesJson)
    {
        var (handler, _) = MakeHandler();
        var paramsJson = ParamsWithRawOptions("{\"copies\":" + copiesJson + "}");

        var act = () => handler.HandleAsync(paramsJson, new ConnectionContext { IsPaired = true }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*copies*");
    }

    [Theory]
    [InlineData("5")]
    [InlineData("null")]
    [InlineData("true")]
    public async Task Handle_PaperSizeWrongType_ThrowsArgumentException(string paperSizeJson)
    {
        var (handler, _) = MakeHandler();
        var paramsJson = ParamsWithRawOptions("{\"paperSize\":" + paperSizeJson + "}");

        var act = () => handler.HandleAsync(paramsJson, new ConnectionContext { IsPaired = true }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*paperSize*");
    }

    [Theory]
    [InlineData("\"A4\"")]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("[]")]
    public async Task Handle_OptionsNotAnObject_ThrowsArgumentException(string optionsJson)
    {
        var (handler, _) = MakeHandler();
        var paramsJson = ParamsWithRawOptions(optionsJson);

        var act = () => handler.HandleAsync(paramsJson, new ConnectionContext { IsPaired = true }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*options*");
    }

    [Fact]
    public async Task Handle_PdfTooLarge_ThrowsArgumentException()
    {
        var handler = new PrintHandler(Substitute.For<IPrintJobSubmitter>(), PrinterServiceFakes.With("HP"), maxBytes: 100);
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
