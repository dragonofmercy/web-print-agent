using FluentAssertions;
using PrintAgent.Tray;
using Xunit;

namespace PrintAgent.Tests.Security;

public class PairingDisplayTests
{
    [Fact]
    public void FormatOriginForDisplay_ConvertsIdnHostToPunycode()
    {
        var safe = PairingPromptForm.FormatOriginForDisplay("https://раураl.com");

        safe.Should().StartWith("https://");
        safe.Should().Contain("xn--"); // Punycode prefix
        safe.Should().NotContain("р"); // cyrillic 'er' should be gone
    }

    [Fact]
    public void FormatOriginForDisplay_StripsBidiAndControlCharacters()
    {
        var safe = PairingPromptForm.FormatOriginForDisplay("https://evil.com‮gogo");

        safe.Should().NotContain("‮");
    }

    [Fact]
    public void FormatOriginForDisplay_PassesThroughAsciiOriginUnchanged()
    {
        var safe = PairingPromptForm.FormatOriginForDisplay("https://example.com:8443");

        safe.Should().Be("https://example.com:8443");
    }

    [Fact]
    public void FormatOriginForDisplay_FallsBackToFilteredInputOnUnparseable()
    {
        var safe = PairingPromptForm.FormatOriginForDisplay("not a uri ");

        safe.Should().NotContain("");
    }
}
