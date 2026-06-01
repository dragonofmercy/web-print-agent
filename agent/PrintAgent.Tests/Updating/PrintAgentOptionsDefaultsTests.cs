using FluentAssertions;
using Xunit;

namespace PrintAgent.Tests.Updating;

public class PrintAgentOptionsDefaultsTests
{
    [Fact]
    public void UpdateDefaults_AreSensible()
    {
        var options = new PrintAgentOptions();

        options.UpdateRepoUrl.Should().Be("https://github.com/dragonofmercy/web-print-agent");
        options.UpdateCheckIntervalHours.Should().Be(6);
        options.UpdateAllowPrerelease.Should().BeFalse();
        options.UpdateInitialDelaySeconds.Should().Be(30);
    }
}
