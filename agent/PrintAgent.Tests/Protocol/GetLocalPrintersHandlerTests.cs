using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using PrintAgent.Printing;
using PrintAgent.Protocol;
using PrintAgent.Protocol.Handlers;
using Xunit;

namespace PrintAgent.Tests.Protocol;

public class GetLocalPrintersHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsListFromPrinterService()
    {
        var printerService = Substitute.For<IPrinterService>();
        printerService.List().Returns(new[]
        {
            new PrinterInfo("HP", true, "Idle", new[] { "A4" }),
            new PrinterInfo("Zebra", false, "Idle", new[] { "100x150" }),
        });
        var handler = new GetLocalPrintersHandler(printerService);
        var conn = new ConnectionContext { IsPaired = true };

        var result = await handler.HandleAsync(null, conn, CancellationToken.None);

        var json = JsonSerializer.SerializeToElement(result);
        json.GetArrayLength().Should().Be(2);
        json[0].GetProperty("name").GetString().Should().Be("HP");
        json[0].GetProperty("isDefault").GetBoolean().Should().BeTrue();
    }
}
