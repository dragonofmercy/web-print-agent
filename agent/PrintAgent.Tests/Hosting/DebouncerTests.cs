using FluentAssertions;
using PrintAgent.Hosting;
using Xunit;

namespace PrintAgent.Tests.Hosting;

public class DebouncerTests
{
    [Fact]
    public async Task RapidTriggers_CollapseToSingleCallback()
    {
        var count = 0;
        var interval = TimeSpan.FromMilliseconds(100);
        using var debouncer = new Debouncer(interval, () => Interlocked.Increment(ref count));

        for (var i = 0; i < 5; i++) debouncer.Trigger();

        await Task.Delay(interval * 4);

        Volatile.Read(ref count).Should().Be(1);
    }

    [Fact]
    public async Task Trigger_FiresCallbackAfterInterval()
    {
        var tcs = new TaskCompletionSource();
        var interval = TimeSpan.FromMilliseconds(100);
        using var debouncer = new Debouncer(interval, () => tcs.TrySetResult());

        debouncer.Trigger();

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(interval * 10));
        completed.Should().Be(tcs.Task, "the callback should fire after the quiet interval");
    }

    [Fact]
    public async Task DisposeBeforeInterval_SuppressesPendingCallback()
    {
        var count = 0;
        var interval = TimeSpan.FromMilliseconds(100);
        var debouncer = new Debouncer(interval, () => Interlocked.Increment(ref count));

        debouncer.Trigger();
        debouncer.Dispose();

        await Task.Delay(interval * 4);

        Volatile.Read(ref count).Should().Be(0);
    }

    [Fact]
    public async Task TriggerAfterDispose_IsNoOp()
    {
        var count = 0;
        var interval = TimeSpan.FromMilliseconds(100);
        var debouncer = new Debouncer(interval, () => Interlocked.Increment(ref count));

        debouncer.Dispose();
        debouncer.Trigger();

        await Task.Delay(interval * 4);

        Volatile.Read(ref count).Should().Be(0);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var debouncer = new Debouncer(TimeSpan.FromMilliseconds(50), () => { });

        var act = () => { debouncer.Dispose(); debouncer.Dispose(); };

        act.Should().NotThrow();
    }
}
