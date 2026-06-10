using System.Net.Http;
using FluentAssertions;
using NSubstitute;
using PrintAgent.Updating;
using Xunit;

namespace PrintAgent.Tests.Updating;

public class UpdateServiceTests
{
    private sealed class FakeUpdateClient : IUpdateClient
    {
        public bool IsInstalled { get; set; } = true;
        public AgentUpdate? PendingUpdate { get; set; }
        public AgentUpdate? CheckResult { get; set; }
        public Exception? CheckThrows { get; set; }
        public TaskCompletionSource? CheckGate { get; set; }

        public int CheckCalls;
        public int DownloadCalls;
        public List<AgentUpdate> Applied { get; } = new();

        public Task<AgentUpdate?> CheckAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref CheckCalls);
            if (CheckThrows is not null) throw CheckThrows;
            return CheckGate is null ? Task.FromResult(CheckResult) : WaitGateAsync();
        }

        private async Task<AgentUpdate?> WaitGateAsync()
        {
            await CheckGate!.Task;
            return CheckResult;
        }

        public Task DownloadAsync(AgentUpdate update, CancellationToken ct)
        {
            Interlocked.Increment(ref DownloadCalls);
            return Task.CompletedTask;
        }

        public void ApplyAndRestart(AgentUpdate update) => Applied.Add(update);
    }

    private static UpdateService NewService(
        FakeUpdateClient client, IUpdateUi ui, bool enabled = true, Func<bool>? hasActiveJobs = null,
        Serilog.ILogger? logger = null, TimeSpan? initialDelay = null, TimeSpan? interval = null)
        => new(client, ui, hasActiveJobs ?? (() => false), enabled,
            initialDelay: initialDelay ?? TimeSpan.FromMinutes(10),
            interval: interval ?? TimeSpan.FromMinutes(10),
            logger: logger ?? Serilog.Core.Logger.None);

    [Fact]
    public async Task Check_FindsUpdate_DownloadsAndNotifiesReady()
    {
        var client = new FakeUpdateClient { CheckResult = new AgentUpdate("1.2.3") };
        var ui = Substitute.For<IUpdateUi>();
        using var svc = NewService(client, ui);

        await svc.CheckNowAsync(manual: false);

        client.DownloadCalls.Should().Be(1);
        ui.Received(1).NotifyUpdateReady("1.2.3");
    }

    [Fact]
    public async Task Check_NoUpdate_Manual_NotifiesUpToDate()
    {
        var client = new FakeUpdateClient { CheckResult = null };
        var ui = Substitute.For<IUpdateUi>();
        using var svc = NewService(client, ui);

        await svc.CheckNowAsync(manual: true);

        client.DownloadCalls.Should().Be(0);
        ui.Received(1).NotifyUpToDate();
    }

    [Fact]
    public async Task Check_NoUpdate_Automatic_IsSilent()
    {
        var client = new FakeUpdateClient { CheckResult = null };
        var ui = Substitute.For<IUpdateUi>();
        using var svc = NewService(client, ui);

        await svc.CheckNowAsync(manual: false);

        ui.DidNotReceive().NotifyUpToDate();
        ui.DidNotReceive().NotifyUpdateReady(Arg.Any<string>());
    }

    [Fact]
    public async Task Check_ClientThrows_IsSwallowed_NoUi()
    {
        var client = new FakeUpdateClient { CheckThrows = new HttpRequestException("offline") };
        var ui = Substitute.For<IUpdateUi>();
        using var svc = NewService(client, ui);

        var act = () => svc.CheckNowAsync(manual: true);

        await act.Should().NotThrowAsync();
        ui.DidNotReceive().NotifyUpToDate();
        ui.DidNotReceive().NotifyUpdateReady(Arg.Any<string>());
        client.DownloadCalls.Should().Be(0);
        client.Applied.Should().BeEmpty();
    }

    [Fact]
    public async Task Disabled_StartAsync_IsNoOp()
    {
        var client = new FakeUpdateClient { PendingUpdate = new AgentUpdate("9.9.9"), CheckResult = new AgentUpdate("9.9.9") };
        var ui = Substitute.For<IUpdateUi>();
        using var svc = NewService(client, ui, enabled: false);

        await svc.StartAsync(CancellationToken.None);

        client.CheckCalls.Should().Be(0);
        client.Applied.Should().BeEmpty();
    }

    [Fact]
    public async Task NotInstalled_StartAsync_IsNoOp()
    {
        var client = new FakeUpdateClient { IsInstalled = false, PendingUpdate = new AgentUpdate("9.9.9") };
        var ui = Substitute.For<IUpdateUi>();
        using var svc = NewService(client, ui);

        await svc.StartAsync(CancellationToken.None);

        client.CheckCalls.Should().Be(0);
        client.Applied.Should().BeEmpty();
    }

    [Fact]
    public async Task UserRestart_Idle_AppliesUpdate()
    {
        var client = new FakeUpdateClient { CheckResult = new AgentUpdate("2.0.0") };
        var ui = Substitute.For<IUpdateUi>();
        using var svc = NewService(client, ui, hasActiveJobs: () => false);

        await svc.CheckNowAsync(manual: false); // stages 2.0.0
        svc.OnUserWantsRestart();

        client.Applied.Should().ContainSingle().Which.Version.Should().Be("2.0.0");
        ui.DidNotReceive().NotifyBusyDeferred();
    }

    [Fact]
    public async Task UserRestart_Busy_DefersAndNotifies()
    {
        var client = new FakeUpdateClient { CheckResult = new AgentUpdate("2.0.0") };
        var ui = Substitute.For<IUpdateUi>();
        using var svc = NewService(client, ui, hasActiveJobs: () => true);

        await svc.CheckNowAsync(manual: false); // stages 2.0.0
        svc.OnUserWantsRestart();

        client.Applied.Should().BeEmpty();
        ui.Received(1).NotifyBusyDeferred();
    }

    [Fact]
    public void UserRestart_NothingStaged_DoesNothing()
    {
        var client = new FakeUpdateClient();
        var ui = Substitute.For<IUpdateUi>();
        using var svc = NewService(client, ui);

        svc.OnUserWantsRestart();

        client.Applied.Should().BeEmpty();
        ui.DidNotReceive().NotifyBusyDeferred();
    }

    [Fact]
    public async Task Start_PendingUpdate_Idle_AppliesSilently()
    {
        var client = new FakeUpdateClient { PendingUpdate = new AgentUpdate("3.0.0") };
        var ui = Substitute.For<IUpdateUi>();
        using var svc = NewService(client, ui, hasActiveJobs: () => false);

        await svc.StartAsync(CancellationToken.None);

        client.Applied.Should().ContainSingle().Which.Version.Should().Be("3.0.0");
    }

    [Fact]
    public async Task Start_PendingUpdate_Busy_DefersSilently()
    {
        var client = new FakeUpdateClient { PendingUpdate = new AgentUpdate("3.0.0") };
        var ui = Substitute.For<IUpdateUi>();
        using var svc = NewService(client, ui, hasActiveJobs: () => true);

        await svc.StartAsync(CancellationToken.None);

        client.Applied.Should().BeEmpty();
        ui.DidNotReceive().NotifyBusyDeferred(); // boot path is silent, no toast
    }

    [Fact]
    public async Task RunLoop_CheckThrowsUnexpectedly_SurvivesAndKeepsChecking()
    {
        // NotifyUpdateReady throwing is an exception OUTSIDE CheckNowAsync's inner try blocks:
        // it must not kill the periodic loop. We prove a SUBSEQUENT check still runs.
        var client = new FakeUpdateClient { CheckResult = new AgentUpdate("1.0.0") };
        var ui = Substitute.For<IUpdateUi>();
        ui.When(u => u.NotifyUpdateReady(Arg.Any<string>())).Throw(new InvalidOperationException("toast failed"));
        var logger = Substitute.For<Serilog.ILogger>();

        using var svc = NewService(client, ui, logger: logger,
            initialDelay: TimeSpan.FromMilliseconds(1), interval: TimeSpan.FromMilliseconds(1));

        await svc.StartAsync(CancellationToken.None);

        // The loop should perform multiple checks despite the first one throwing from the UI.
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
        while (client.CheckCalls < 2 && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(10);

        client.CheckCalls.Should().BeGreaterThanOrEqualTo(2,
            "a single failed cycle must not terminate the periodic update loop");
    }

    [Fact]
    public async Task RunLoop_CheckThrowsUnexpectedly_IsLoggedAtWarning()
    {
        var client = new FakeUpdateClient { CheckResult = new AgentUpdate("1.0.0") };
        var ui = Substitute.For<IUpdateUi>();
        var boom = new InvalidOperationException("toast failed");
        ui.When(u => u.NotifyUpdateReady(Arg.Any<string>())).Throw(boom);
        var logger = Substitute.For<Serilog.ILogger>();

        using var svc = NewService(client, ui, logger: logger,
            initialDelay: TimeSpan.FromMilliseconds(1), interval: TimeSpan.FromMilliseconds(1));

        await svc.StartAsync(CancellationToken.None);

        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
        while (client.CheckCalls < 1 && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(10);
        // Give the catch a moment to log after the throw.
        await Task.Delay(50);

        logger.Received().Warning(boom, Arg.Any<string>());
    }

    [Fact]
    public async Task RunLoop_AfterFailedCycle_ServiceRemainsUsableForManualCheck()
    {
        // First cycle throws from the UI; once that cycle is past, the service must still
        // service a manual check (it is not permanently dead). Here the UI behaves on the
        // second call, so a manual check completes and notifies normally.
        var client = new FakeUpdateClient { CheckResult = new AgentUpdate("1.0.0") };
        var ui = Substitute.For<IUpdateUi>();
        var calls = 0;
        ui.When(u => u.NotifyUpdateReady(Arg.Any<string>())).Do(_ =>
        {
            if (Interlocked.Increment(ref calls) == 1) throw new InvalidOperationException("toast failed");
        });
        var logger = Substitute.For<Serilog.ILogger>();

        using var svc = NewService(client, ui, logger: logger,
            initialDelay: TimeSpan.FromMilliseconds(1), interval: TimeSpan.FromMinutes(10));

        await svc.StartAsync(CancellationToken.None);

        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
        while (client.CheckCalls < 1 && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(10);

        // The service must still respond to a manual check after a background failure.
        var act = () => svc.CheckNowAsync(manual: true);
        await act.Should().NotThrowAsync();
        ui.Received(2).NotifyUpdateReady("1.0.0");
    }

    [Fact]
    public async Task Check_SingleFlight_ConcurrentCheckIsSkipped()
    {
        var gate = new TaskCompletionSource();
        var client = new FakeUpdateClient { CheckGate = gate, CheckResult = new AgentUpdate("4.0.0") };
        var ui = Substitute.For<IUpdateUi>();
        using var svc = NewService(client, ui);

        var first = svc.CheckNowAsync(manual: false); // enters, blocks on the gate

        // Wait until the first check is actually inside CheckAsync.
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(2);
        while (client.CheckCalls == 0 && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(10);

        await svc.CheckNowAsync(manual: false); // should be skipped by single-flight, returns immediately
        client.CheckCalls.Should().Be(1);

        gate.SetResult();
        await first;
        client.DownloadCalls.Should().Be(1);
    }
}
