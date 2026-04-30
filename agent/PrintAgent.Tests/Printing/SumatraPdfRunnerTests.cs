using FluentAssertions;
using PrintAgent.Printing;
using Xunit;

namespace PrintAgent.Tests.Printing;

public class SumatraPdfRunnerTests
{
    [Fact]
    public void BuildArguments_DefaultOptions_HasSilentAndPrinterAndPath()
    {
        var args = SumatraPdfRunner.BuildArguments("HP LaserJet", "C:\\temp\\file.pdf", new PrintOptions());

        args.Should().ContainInOrder(
            "-print-to", "HP LaserJet",
            "-silent",
            "C:\\temp\\file.pdf");
    }

    [Fact]
    public void BuildArguments_ThreeCopies_AddsCopiesSetting()
    {
        var args = SumatraPdfRunner.BuildArguments("HP", "x.pdf", new PrintOptions(Copies: 3));

        args.Should().Contain("-print-settings");
        args.Should().Contain("3x");
    }

    [Fact]
    public void BuildArguments_MonoColorAndA4_CombinesWithCommas()
    {
        var args = SumatraPdfRunner.BuildArguments("HP", "x.pdf",
            new PrintOptions(Copies: 2, PaperSize: "A4", Color: false));

        var idx = args.IndexOf("-print-settings");
        idx.Should().BeGreaterOrEqualTo(0);
        args[idx + 1].Should().Be("2x,paper=A4,monochrome");
    }

    [Fact]
    public void BuildArguments_NoExtraOptions_OmitsPrintSettings()
    {
        var args = SumatraPdfRunner.BuildArguments("HP", "x.pdf", new PrintOptions());

        args.Should().NotContain("-print-settings");
    }

    [Fact]
    public void BuildArguments_LandscapeOrientation_AddsLandscapeSetting()
    {
        var args = SumatraPdfRunner.BuildArguments("HP", "x.pdf",
            new PrintOptions(Orientation: PrintOrientation.Landscape));

        var idx = args.IndexOf("-print-settings");
        idx.Should().BeGreaterOrEqualTo(0);
        args[idx + 1].Should().Be("landscape");
    }

    [Fact]
    public void BuildArguments_PortraitOrientation_AddsPortraitSetting()
    {
        var args = SumatraPdfRunner.BuildArguments("HP", "x.pdf",
            new PrintOptions(Orientation: PrintOrientation.Portrait));

        var idx = args.IndexOf("-print-settings");
        idx.Should().BeGreaterOrEqualTo(0);
        args[idx + 1].Should().Be("portrait");
    }

    [Fact]
    public void BuildArguments_OrientationCombinedWithOtherSettings_AppendsAtTheEnd()
    {
        var args = SumatraPdfRunner.BuildArguments("HP", "x.pdf",
            new PrintOptions(Copies: 2, PaperSize: "A4", Color: false, Orientation: PrintOrientation.Landscape));

        var idx = args.IndexOf("-print-settings");
        idx.Should().BeGreaterOrEqualTo(0);
        args[idx + 1].Should().Be("2x,paper=A4,monochrome,landscape");
    }
}
