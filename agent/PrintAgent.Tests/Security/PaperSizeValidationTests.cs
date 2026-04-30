using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using PrintAgent.Printing;
using PrintAgent.Protocol;
using PrintAgent.Protocol.Handlers;
using PrintAgent.Tests.Helpers;
using Xunit;

namespace PrintAgent.Tests.Security;

public class PaperSizeValidationTests
{
    private static JsonElement MakeParams(string? paperSize) => JsonSerializer.SerializeToElement(new
    {
        printerName = "HP",
        pdfBase64 = Convert.ToBase64String("%PDF-1.4\n%%EOF"u8.ToArray()),
        options = new { paperSize }
    });

    [Theory]
    [InlineData("A4,2x,monochrome")]
    [InlineData("A4=foo")]
    [InlineData("A4\nLetter")]
    [InlineData("A4;Letter")]
    [InlineData("../etc/passwd")]
    [InlineData("-monochrome")]
    public async Task Handle_PaperSizeWithDisallowedCharacters_ThrowsArgumentException(string paperSize)
    {
        var jobs = Substitute.For<IPrintJobSubmitter>();
        var handler = new PrintHandler(jobs, PrinterServiceFakes.With("HP"), maxBytes: 1024);

        var act = () => handler.HandleAsync(MakeParams(paperSize),
            new ConnectionContext { IsPaired = true }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*paperSize*");
    }

    [Theory]
    [InlineData("A4")]
    [InlineData("Letter")]
    [InlineData("A3 Extra")]
    [InlineData("US-Letter")]
    [InlineData("env_10")]
    public async Task Handle_PaperSizeWithSafeCharacters_Accepted(string paperSize)
    {
        var jobs = Substitute.For<IPrintJobSubmitter>();
        jobs.SubmitAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<PrintOptions>(),
                Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());
        var handler = new PrintHandler(jobs, PrinterServiceFakes.With("HP"), maxBytes: 1024);

        await handler.HandleAsync(MakeParams(paperSize),
            new ConnectionContext { IsPaired = true }, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_PaperSizeTooLong_ThrowsArgumentException()
    {
        var jobs = Substitute.For<IPrintJobSubmitter>();
        var handler = new PrintHandler(jobs, PrinterServiceFakes.With("HP"), maxBytes: 1024);

        var act = () => handler.HandleAsync(MakeParams(new string('A', 33)),
            new ConnectionContext { IsPaired = true }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*paperSize*");
    }
}
