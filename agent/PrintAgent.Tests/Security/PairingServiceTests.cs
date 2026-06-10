using FluentAssertions;
using NSubstitute;
using PrintAgent.Security;
using PrintAgent.Storage;
using PrintAgent.Tests.Helpers;
using Xunit;

namespace PrintAgent.Tests.Security;

public class PairingServiceTests
{
    private static (PairingService svc, IPairingUi ui, ConfigStore store) Build(TempDirectory temp,
        TimeSpan? cooldown = null)
    {
        var ui = Substitute.For<IPairingUi>();
        var store = new ConfigStore(Path.Combine(temp.Path, "config.json"));
        var svc = new PairingService(store, ui, cooldown ?? TimeSpan.FromMinutes(5),
            timeout: TimeSpan.FromSeconds(60));
        return (svc, ui, store);
    }

    [Fact]
    public async Task RequestApproval_AlreadyAllowed_ShortCircuitsToApproved()
    {
        using var temp = new TempDirectory();
        var (svc, ui, store) = Build(temp);
        store.AddAllowedOrigin("https://app.example.com");

        var decision = await svc.RequestApprovalAsync("https://app.example.com", CancellationToken.None);

        decision.Should().Be(PairingDecision.Approved);
        await ui.DidNotReceive().PromptAsync(default!, default, default);
    }

    [Fact]
    public async Task RequestApproval_UserApproves_PersistsOrigin()
    {
        using var temp = new TempDirectory();
        var (svc, ui, store) = Build(temp);
        ui.PromptAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(PairingDecision.Approved);

        var decision = await svc.RequestApprovalAsync("https://app.example.com", CancellationToken.None);

        decision.Should().Be(PairingDecision.Approved);
        store.IsOriginAllowed("https://app.example.com").Should().BeTrue();
    }

    [Fact]
    public async Task RequestApproval_UserRefuses_DoesNotPersist()
    {
        using var temp = new TempDirectory();
        var (svc, ui, store) = Build(temp);
        ui.PromptAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(PairingDecision.Refused);

        var decision = await svc.RequestApprovalAsync("https://app.example.com", CancellationToken.None);

        decision.Should().Be(PairingDecision.Refused);
        store.IsOriginAllowed("https://app.example.com").Should().BeFalse();
    }

    [Fact]
    public async Task RequestApproval_DuringCooldown_ReturnsRefusedWithoutPrompt()
    {
        using var temp = new TempDirectory();
        var (svc, ui, _) = Build(temp);
        ui.PromptAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(PairingDecision.Refused);

        await svc.RequestApprovalAsync("https://app.example.com", CancellationToken.None);

        ui.ClearReceivedCalls();
        var decision = await svc.RequestApprovalAsync("https://app.example.com", CancellationToken.None);

        decision.Should().Be(PairingDecision.Refused);
        await ui.DidNotReceive().PromptAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestApproval_ConcurrentSameOrigin_PromptsOnceAndSharesDecision()
    {
        using var temp = new TempDirectory();
        var (svc, ui, store) = Build(temp);
        var tcs = new TaskCompletionSource<PairingDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        ui.PromptAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(tcs.Task);

        var first = svc.RequestApprovalAsync("https://app.example.com", CancellationToken.None);
        var second = svc.RequestApprovalAsync("https://app.example.com", CancellationToken.None);

        first.IsCompleted.Should().BeFalse();
        second.IsCompleted.Should().BeFalse();
        tcs.SetResult(PairingDecision.Approved);

        (await first).Should().Be(PairingDecision.Approved);
        (await second).Should().Be(PairingDecision.Approved);
        await ui.Received(1).PromptAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        store.GetAllowedOrigins().Should().ContainSingle().Which.Should().Be("https://app.example.com");
    }

    [Fact]
    public async Task RequestApproval_ConcurrentSameOriginRefused_BothRefusedAndCooldownApplies()
    {
        using var temp = new TempDirectory();
        var (svc, ui, store) = Build(temp);
        var tcs = new TaskCompletionSource<PairingDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        ui.PromptAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(tcs.Task);

        var first = svc.RequestApprovalAsync("https://app.example.com", CancellationToken.None);
        var second = svc.RequestApprovalAsync("https://app.example.com", CancellationToken.None);
        tcs.SetResult(PairingDecision.Refused);

        (await first).Should().Be(PairingDecision.Refused);
        (await second).Should().Be(PairingDecision.Refused);
        store.IsOriginAllowed("https://app.example.com").Should().BeFalse();

        ui.ClearReceivedCalls();
        var third = await svc.RequestApprovalAsync("https://app.example.com", CancellationToken.None);
        third.Should().Be(PairingDecision.Refused);
        await ui.DidNotReceive().PromptAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestApproval_ConcurrentDifferentOrigins_PromptIndependently()
    {
        using var temp = new TempDirectory();
        var (svc, ui, store) = Build(temp);
        var tcsA = new TaskCompletionSource<PairingDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tcsB = new TaskCompletionSource<PairingDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        ui.PromptAsync("https://a.example.com", Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(tcsA.Task);
        ui.PromptAsync("https://b.example.com", Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(tcsB.Task);

        var first = svc.RequestApprovalAsync("https://a.example.com", CancellationToken.None);
        var second = svc.RequestApprovalAsync("https://b.example.com", CancellationToken.None);
        tcsA.SetResult(PairingDecision.Approved);
        tcsB.SetResult(PairingDecision.Refused);

        (await first).Should().Be(PairingDecision.Approved);
        (await second).Should().Be(PairingDecision.Refused);
        await ui.Received(1).PromptAsync("https://a.example.com", Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        await ui.Received(1).PromptAsync("https://b.example.com", Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        store.IsOriginAllowed("https://a.example.com").Should().BeTrue();
        store.IsOriginAllowed("https://b.example.com").Should().BeFalse();
    }

    [Fact]
    public async Task RequestApproval_OneWaiterCancels_OtherWaiterStillGetsSharedDecision()
    {
        using var temp = new TempDirectory();
        var (svc, ui, store) = Build(temp);
        var tcs = new TaskCompletionSource<PairingDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        ui.PromptAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                // Mimic WinFormsPairingUi: token cancellation resolves the prompt as TimedOut.
                callInfo.Arg<CancellationToken>().Register(() => tcs.TrySetResult(PairingDecision.TimedOut));
                return tcs.Task;
            });

        using var cts1 = new CancellationTokenSource();
        using var cts2 = new CancellationTokenSource();
        var first = svc.RequestApprovalAsync("https://app.example.com", cts1.Token);
        var second = svc.RequestApprovalAsync("https://app.example.com", cts2.Token);
        first.IsCompleted.Should().BeFalse();
        second.IsCompleted.Should().BeFalse();

        // Caller 1 disconnects while the prompt is still pending.
        cts1.Cancel();
        await first.Invoking(t => t).Should().ThrowAsync<OperationCanceledException>();
        second.IsCompleted.Should().BeFalse();

        // The shared prompt keeps running and is then approved for caller 2.
        tcs.SetResult(PairingDecision.Approved);
        (await second).Should().Be(PairingDecision.Approved);
        store.IsOriginAllowed("https://app.example.com").Should().BeTrue();
    }

    [Fact]
    public async Task RequestApproval_AfterPendingPromptResolves_NextRequestPromptsAgain()
    {
        using var temp = new TempDirectory();
        var (svc, ui, _) = Build(temp, cooldown: TimeSpan.Zero);
        ui.PromptAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(PairingDecision.Refused);

        await svc.RequestApprovalAsync("https://app.example.com", CancellationToken.None);
        await svc.RequestApprovalAsync("https://app.example.com", CancellationToken.None);

        await ui.Received(2).PromptAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestApproval_AfterCooldownExpires_PromptsAgain()
    {
        using var temp = new TempDirectory();
        var (svc, ui, _) = Build(temp, cooldown: TimeSpan.FromMilliseconds(50));
        ui.PromptAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(PairingDecision.Refused, PairingDecision.Approved);

        await svc.RequestApprovalAsync("https://app.example.com", CancellationToken.None);
        await Task.Delay(80);
        ui.ClearReceivedCalls();

        var decision = await svc.RequestApprovalAsync("https://app.example.com", CancellationToken.None);

        decision.Should().Be(PairingDecision.Approved);
        await ui.Received(1).PromptAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }
}
