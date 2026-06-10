using FluentAssertions;
using PrintAgent.Hosting;
using Xunit;

namespace PrintAgent.Tests.Hosting;

public class SendGateTests
{
    [Fact]
    public async Task RunAsync_ConcurrentCalls_NeverOverlap()
    {
        using var gate = new SendGate();
        var active = 0;
        var maxActive = 0;

        var tasks = Enumerable.Range(0, 20).Select(_ => gate.RunAsync(async () =>
        {
            var now = Interlocked.Increment(ref active);
            InterlockedMax(ref maxActive, now);
            await Task.Yield();
            Interlocked.Decrement(ref active);
        }, default));

        await Task.WhenAll(tasks);

        maxActive.Should().Be(1);
    }

    [Fact]
    public async Task RunAsync_AfterOperationThrows_GateIsReleased()
    {
        using var gate = new SendGate();

        var act = () => gate.RunAsync(() => throw new InvalidOperationException("boom"), default);
        await act.Should().ThrowAsync<InvalidOperationException>();

        var ran = false;
        await gate.RunAsync(() => { ran = true; return Task.CompletedTask; }, default);

        ran.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_CancelledWhileWaiting_DoesNotRunOperation()
    {
        using var gate = new SendGate();
        var firstEntered = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var holder = gate.RunAsync(async () => { firstEntered.SetResult(); await release.Task; }, default);
        await firstEntered.Task;

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var ran = false;
        var act = () => gate.RunAsync(() => { ran = true; return Task.CompletedTask; }, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        ran.Should().BeFalse();

        release.SetResult();
        await holder;
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while (value > (current = Volatile.Read(ref target)))
            Interlocked.CompareExchange(ref target, value, current);
    }
}
