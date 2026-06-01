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
        FakeUpdateClient client, IUpdateUi ui, bool enabled = true, Func<bool>? hasActiveJobs = null)
        => new(client, ui, hasActiveJobs ?? (() => false), enabled,
            initialDelay: TimeSpan.FromMinutes(10), interval: TimeSpan.FromMinutes(10),
            logger: Serilog.Core.Logger.None);

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
}
