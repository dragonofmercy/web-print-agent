using FluentAssertions;
using PrintAgent.Printing;
using Xunit;

namespace PrintAgent.Tests.Printing;

public class JobErrorSanitizationTests
{
    [Fact]
    public void Sanitize_StripsControlCharacters()
    {
        var raw = "fail at \x07 C:\\path \x1b-- end";

        var clean = PrintJobService.SanitizeError(raw);

        clean.Should().NotContain("\x07");
        clean.Should().NotContain("\x1b");
        clean.Should().Contain("fail at");
        clean.Should().Contain("end");
    }

    [Fact]
    public void Sanitize_TruncatesLongMessagesTo256Chars()
    {
        var raw = new string('x', 1024);

        var clean = PrintJobService.SanitizeError(raw);

        clean!.Length.Should().BeLessOrEqualTo(256);
    }

    [Fact]
    public void Sanitize_NullOrWhitespace_ReturnsNull()
    {
        PrintJobService.SanitizeError(null).Should().BeNull();
        PrintJobService.SanitizeError("   ").Should().BeNull();
    }
}
