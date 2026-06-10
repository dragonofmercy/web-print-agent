using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using PrintAgent.Protocol;
using Xunit;

namespace PrintAgent.Tests.Protocol;

public class RpcRouterTests
{
    private static ConnectionContext PairedConn() => new() { Origin = "https://x.test", IsPaired = true };
    private static ConnectionContext UnpairedConn() => new() { Origin = "https://x.test", IsPaired = false };

    [Fact]
    public async Task Dispatch_KnownMethod_InvokesHandlerAndReturnsResult()
    {
        var handler = Substitute.For<IRpcHandler>();
        handler.Method.Returns("foo");
        handler.RequiresPairedConnection.Returns(true);
        handler.HandleAsync(Arg.Any<JsonElement?>(), Arg.Any<ConnectionContext>(), Arg.Any<CancellationToken>())
            .Returns(new { ok = true });
        var router = new RpcRouter(new[] { handler });

        var request = """{"jsonrpc":"2.0","id":1,"method":"foo","params":{}}""";
        var response = await router.DispatchAsync(request, PairedConn(), CancellationToken.None);

        response.Should().Contain("\"result\"").And.Contain("\"ok\":true");
    }

    [Fact]
    public async Task Dispatch_UnknownMethod_ReturnsMethodNotFound()
    {
        var router = new RpcRouter(Array.Empty<IRpcHandler>());

        var response = await router.DispatchAsync(
            """{"jsonrpc":"2.0","id":1,"method":"nope"}""",
            PairedConn(), CancellationToken.None);

        response.Should().Contain("\"code\":-32601");
    }

    [Fact]
    public async Task Dispatch_MalformedJson_ReturnsInvalidRequest()
    {
        var router = new RpcRouter(Array.Empty<IRpcHandler>());

        var response = await router.DispatchAsync("{ not json", PairedConn(), CancellationToken.None);

        response.Should().Contain("\"code\":-32600");
    }

    [Fact]
    public async Task Dispatch_PairingRequiredOnUnpairedConnection_ReturnsOriginNotAuthorized()
    {
        var handler = Substitute.For<IRpcHandler>();
        handler.Method.Returns("secret");
        handler.RequiresPairedConnection.Returns(true);
        var router = new RpcRouter(new[] { handler });

        var response = await router.DispatchAsync(
            """{"jsonrpc":"2.0","id":1,"method":"secret"}""",
            UnpairedConn(), CancellationToken.None);

        response.Should().Contain("\"code\":-32000");
    }

    [Fact]
    public async Task Dispatch_HandlerThrowsArgumentException_ReturnsInvalidParams()
    {
        var handler = Substitute.For<IRpcHandler>();
        handler.Method.Returns("foo");
        handler.RequiresPairedConnection.Returns(false);
        handler.HandleAsync(Arg.Any<JsonElement?>(), Arg.Any<ConnectionContext>(), Arg.Any<CancellationToken>())
            .Returns<object?>(_ => throw new ArgumentException("bad params"));
        var router = new RpcRouter(new[] { handler });

        var response = await router.DispatchAsync(
            """{"jsonrpc":"2.0","id":1,"method":"foo"}""",
            UnpairedConn(), CancellationToken.None);

        response.Should().Contain("\"code\":-32602");
    }

    [Fact]
    public async Task Dispatch_HandlerThrowsGenericException_ReturnsGenericInternalErrorWithoutMessageContents()
    {
        var handler = Substitute.For<IRpcHandler>();
        handler.Method.Returns("foo");
        handler.RequiresPairedConnection.Returns(false);
        handler.HandleAsync(Arg.Any<JsonElement?>(), Arg.Any<ConnectionContext>(), Arg.Any<CancellationToken>())
            .Returns<object?>(_ => throw new InvalidOperationException("C:\\Users\\dzeller\\secret\\path.dll"));
        var router = new RpcRouter(new[] { handler });

        var response = await router.DispatchAsync(
            """{"jsonrpc":"2.0","id":1,"method":"foo"}""",
            UnpairedConn(), CancellationToken.None);

        response.Should().NotContain("C:\\\\Users");
        response.Should().NotContain("secret");
        response.Should().Contain("Internal error");
    }

    [Fact]
    public async Task Dispatch_HandlerThrowsGenericException_ReturnsInternalErrorCode()
    {
        var handler = Substitute.For<IRpcHandler>();
        handler.Method.Returns("foo");
        handler.RequiresPairedConnection.Returns(false);
        handler.HandleAsync(Arg.Any<JsonElement?>(), Arg.Any<ConnectionContext>(), Arg.Any<CancellationToken>())
            .Returns<object?>(_ => throw new InvalidOperationException("boom"));
        var router = new RpcRouter(new[] { handler });

        var response = await router.DispatchAsync(
            """{"jsonrpc":"2.0","id":1,"method":"foo"}""",
            UnpairedConn(), CancellationToken.None);

        response.Should().Contain("\"code\":-32603");
    }

    [Fact]
    public async Task Dispatch_CancelledConnection_RethrowsWithoutBuildingErrorResponse()
    {
        var handler = Substitute.For<IRpcHandler>();
        handler.Method.Returns("foo");
        handler.RequiresPairedConnection.Returns(false);
        handler.HandleAsync(Arg.Any<JsonElement?>(), Arg.Any<ConnectionContext>(), Arg.Any<CancellationToken>())
            .Returns<object?>(callInfo => throw new OperationCanceledException(callInfo.Arg<CancellationToken>()));
        var router = new RpcRouter(new[] { handler });

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => router.DispatchAsync(
            """{"jsonrpc":"2.0","id":1,"method":"foo"}""",
            UnpairedConn(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
