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
