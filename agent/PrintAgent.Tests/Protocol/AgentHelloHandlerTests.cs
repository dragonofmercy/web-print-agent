using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using PrintAgent.Protocol;
using PrintAgent.Protocol.Handlers;
using PrintAgent.Security;
using Xunit;

namespace PrintAgent.Tests.Protocol;

public class AgentHelloHandlerTests
{
    private static ConnectionContext NewConn(string origin) => new() { Origin = origin };

    [Fact]
    public async Task Handle_AlreadyAllowedOrigin_MarksConnectionPairedAndReturnsCapabilities()
    {
        var pairing = Substitute.For<IPairingCoordinator>();
        pairing.RequestApprovalAsync("https://x.test", Arg.Any<CancellationToken>())
            .Returns(PairingDecision.Approved);
        var handler = new AgentHelloHandler(pairing);
        var conn = NewConn("https://x.test");

        var result = await handler.HandleAsync(null, conn, CancellationToken.None);

        conn.IsPaired.Should().BeTrue();
        var json = JsonSerializer.SerializeToElement(result);
        json.GetProperty("agentVersion").GetString().Should().NotBeNullOrEmpty();
        json.GetProperty("jobEventsSupported").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Handle_OriginRefused_ThrowsRpcOriginNotAuthorized()
    {
        var pairing = Substitute.For<IPairingCoordinator>();
        pairing.RequestApprovalAsync("https://x.test", Arg.Any<CancellationToken>())
            .Returns(PairingDecision.Refused);
        var handler = new AgentHelloHandler(pairing);
        var conn = NewConn("https://x.test");

        var act = () => handler.HandleAsync(null, conn, CancellationToken.None);

        await act.Should().ThrowAsync<RpcApplicationException>()
            .Where(e => e.Code == JsonRpcErrorCodes.OriginNotAuthorized);
    }
}
