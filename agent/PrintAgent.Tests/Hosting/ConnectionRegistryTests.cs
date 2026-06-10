using System.Collections.Concurrent;
using FluentAssertions;
using PrintAgent.Hosting;
using PrintAgent.Protocol;
using Xunit;

namespace PrintAgent.Tests.Hosting;

public class ConnectionRegistryTests
{
    private static readonly JsonRpcNotification PrintersChanged =
        new() { Method = "printers.changed", Params = new { } };

    private static (ConnectionContext ctx, List<JsonRpcNotification> received) MakeRecording(bool paired)
    {
        var received = new List<JsonRpcNotification>();
        var ctx = new ConnectionContext
        {
            IsPaired = paired,
            SendNotificationAsync = (n, _) =>
            {
                lock (received) received.Add(n);
                return Task.CompletedTask;
            }
        };
        return (ctx, received);
    }

    [Fact]
    public async Task BroadcastToPaired_ReachesAllPairedConnections()
    {
        var registry = new ConnectionRegistry();
        var (a, recA) = MakeRecording(paired: true);
        var (b, recB) = MakeRecording(paired: true);
        registry.Add(a);
        registry.Add(b);

        await registry.BroadcastToPairedAsync(PrintersChanged, CancellationToken.None);

        recA.Should().ContainSingle().Which.Method.Should().Be("printers.changed");
        recB.Should().ContainSingle().Which.Method.Should().Be("printers.changed");
    }

    [Fact]
    public async Task BroadcastToPaired_SkipsUnpairedConnections()
    {
        var registry = new ConnectionRegistry();
        var (paired, recPaired) = MakeRecording(paired: true);
        var (unpaired, recUnpaired) = MakeRecording(paired: false);
        registry.Add(paired);
        registry.Add(unpaired);

        await registry.BroadcastToPairedAsync(PrintersChanged, CancellationToken.None);

        recPaired.Should().ContainSingle();
        recUnpaired.Should().BeEmpty();
    }

    [Fact]
    public async Task BroadcastToPaired_OneThrowingConnection_DoesNotPreventOthers()
    {
        var registry = new ConnectionRegistry();
        var throwing = new ConnectionContext
        {
            IsPaired = true,
            SendNotificationAsync = (_, _) => throw new InvalidOperationException("dead socket")
        };
        var (good1, rec1) = MakeRecording(paired: true);
        var (good2, rec2) = MakeRecording(paired: true);
        registry.Add(throwing);
        registry.Add(good1);
        registry.Add(good2);

        var act = async () => await registry.BroadcastToPairedAsync(PrintersChanged, CancellationToken.None);

        await act.Should().NotThrowAsync();
        rec1.Should().ContainSingle();
        rec2.Should().ContainSingle();
    }

    [Fact]
    public async Task BroadcastToPaired_RemovedConnection_ReceivesNothing()
    {
        var registry = new ConnectionRegistry();
        var (a, recA) = MakeRecording(paired: true);
        registry.Add(a);
        registry.Remove(a.ConnectionId);

        await registry.BroadcastToPairedAsync(PrintersChanged, CancellationToken.None);

        recA.Should().BeEmpty();
        registry.Count.Should().Be(0);
    }

    [Fact]
    public async Task BroadcastToPaired_NoConnections_CompletesImmediately()
    {
        var registry = new ConnectionRegistry();

        var act = async () => await registry.BroadcastToPairedAsync(PrintersChanged, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ConcurrentAddAndBroadcast_DoesNotThrow()
    {
        var registry = new ConnectionRegistry();
        var sink = new ConcurrentBag<JsonRpcNotification>();

        var adders = Enumerable.Range(0, 50).Select(_ => Task.Run(() =>
        {
            registry.Add(new ConnectionContext
            {
                IsPaired = true,
                SendNotificationAsync = (n, _) => { sink.Add(n); return Task.CompletedTask; }
            });
        }));

        var broadcasters = Enumerable.Range(0, 50).Select(_ => Task.Run(async () =>
            await registry.BroadcastToPairedAsync(PrintersChanged, CancellationToken.None)));

        var act = async () => await Task.WhenAll(adders.Concat(broadcasters));

        await act.Should().NotThrowAsync();
    }
}
