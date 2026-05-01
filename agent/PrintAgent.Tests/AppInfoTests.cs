using FluentAssertions;
using PrintAgent;
using Xunit;

namespace PrintAgent.Tests;

public class AppInfoTests
{
    [Fact]
    public void Version_DoesNotContainCommitShaSuffix()
    {
        AppInfo.Version.Should().NotContain("+");
    }

    [Fact]
    public void Version_IsThreePartSemVer()
    {
        // Expect "X.Y.Z" with three dot-separated numeric parts.
        var parts = AppInfo.Version.Split('.');
        parts.Length.Should().Be(3);
        foreach (var part in parts)
            int.TryParse(part, out _).Should().BeTrue($"expected numeric part but got '{part}'");
    }

    [Fact]
    public void Version_IsNonEmpty()
    {
        AppInfo.Version.Should().NotBeNullOrWhiteSpace();
    }
}
