using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using FluentAssertions;
using NSubstitute;
using PrintAgent.Hosting;
using PrintAgent.Printing;
using PrintAgent.Protocol;
using PrintAgent.Protocol.Events;
using PrintAgent.Protocol.Handlers;
using PrintAgent.Security;
using PrintAgent.Storage;
using PrintAgent.Tests.Helpers;
using Serilog;
using Xunit;

namespace PrintAgent.Tests.Hosting;

public class KestrelHostIntegrationTests
{
    [FactWindowsOnly]
    public async Task GetLocalPrinters_RoundTripsOverWss_ReturnsList()
    {
        using var temp = new TempDirectory();
        var configStore = new ConfigStore(Path.Combine(temp.Path, "config.json"));
        configStore.AddAllowedOrigin("https://test.localhost");

        var origins = new OriginAuthorizationService(configStore, allowInsecureOrigins: false);
        var publisher = new JobEventPublisher();
        var pairing = Substitute.For<IPairingCoordinator>();
        pairing.RequestApprovalAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(PairingDecision.Approved);

        var printerService = Substitute.For<IPrinterService>();
        printerService.List().Returns(new[] { new PrinterInfo("HP", true, "Idle", new[] { "A4" }, new[] { "Tray 1" }) });

        var router = new RpcRouter(new IRpcHandler[]
        {
            new AgentHelloHandler(pairing),
            new GetLocalPrintersHandler(printerService),
        });

        var cert = CertificateService.GenerateSelfSigned();
        var endpoint = new WebSocketEndpoint(router, origins, publisher,
            Log.Logger, maxMessageBytes: 1024 * 1024);

        var host = new KestrelHost();
        await host.StartAsync(new[] { 0 }, cert, endpoint, Log.Logger, CancellationToken.None);
        host.BoundPort.Should().BeGreaterThan(0);

        try
        {
            using var client = new ClientWebSocket();
            client.Options.SetRequestHeader("Origin", "https://test.localhost");
            client.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;

            await client.ConnectAsync(new Uri($"wss://127.0.0.1:{host.BoundPort}/ws"), CancellationToken.None);

            var hello = """{"jsonrpc":"2.0","id":1,"method":"agent.hello"}""";
            await client.SendAsync(Encoding.UTF8.GetBytes(hello), WebSocketMessageType.Text, true, CancellationToken.None);
            var helloResp = await ReceiveTextAsync(client);
            helloResp.Should().Contain("\"agentVersion\"");

            var listMsg = """{"jsonrpc":"2.0","id":2,"method":"getLocalPrinters"}""";
            await client.SendAsync(Encoding.UTF8.GetBytes(listMsg), WebSocketMessageType.Text, true, CancellationToken.None);
            var listResp = await ReceiveTextAsync(client);
            listResp.Should().Contain("\"name\":\"HP\"");
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    private static async Task<string> ReceiveTextAsync(ClientWebSocket ws)
    {
        var buffer = new byte[8192];
        using var ms = new MemoryStream();
        WebSocketReceiveResult res;
        do
        {
            res = await ws.ReceiveAsync(buffer, CancellationToken.None);
            ms.Write(buffer, 0, res.Count);
        } while (!res.EndOfMessage);
        return Encoding.UTF8.GetString(ms.ToArray());
    }
}
