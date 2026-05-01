using System.Security.Cryptography;
using FluentAssertions;
using PrintAgent.Tests.Helpers;
using Xunit;

namespace PrintAgent.Tests.Hosting;

public class SumatraExtractionTests
{
    [Fact]
    public void ShaMatch_ReturnsTrueWhenFileHashEqualsExpected()
    {
        using var tmp = new TempDirectory();
        var path = Path.Combine(tmp.Path, "f.bin");
        File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4 });
        var expected = SHA256.HashData(new byte[] { 1, 2, 3, 4 });

        PrintAgent.SumatraExtraction.FileMatchesSha256(path, expected).Should().BeTrue();
    }

    [Fact]
    public void ShaMatch_ReturnsFalseWhenFileHashDiffers()
    {
        using var tmp = new TempDirectory();
        var path = Path.Combine(tmp.Path, "f.bin");
        File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4 });
        var expected = SHA256.HashData(new byte[] { 9, 9, 9, 9 });

        PrintAgent.SumatraExtraction.FileMatchesSha256(path, expected).Should().BeFalse();
    }

    [Fact]
    public void ShaMatch_ReturnsFalseWhenFileMissing()
    {
        var fake = Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid().ToString("N"));
        var expected = new byte[32];

        PrintAgent.SumatraExtraction.FileMatchesSha256(fake, expected).Should().BeFalse();
    }
}
