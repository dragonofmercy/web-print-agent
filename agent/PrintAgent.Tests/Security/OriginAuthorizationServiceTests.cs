using FluentAssertions;
using PrintAgent.Security;
using PrintAgent.Storage;
using PrintAgent.Tests.Helpers;
using Xunit;

namespace PrintAgent.Tests.Security;

public class OriginAuthorizationServiceTests
{
    private static OriginAuthorizationService Build(TempDirectory temp, bool allowInsecure = false)
    {
        var configStore = new ConfigStore(Path.Combine(temp.Path, "config.json"));
        return new OriginAuthorizationService(configStore, allowInsecure);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com")]
    public void Classify_InvalidOrigin_Rejects(string origin)
    {
        using var temp = new TempDirectory();
        Build(temp).Classify(origin).Should().Be(OriginClassification.Rejected);
    }

    [Fact]
    public void Classify_HttpOrigin_RejectedWhenAllowInsecureFalse()
    {
        using var temp = new TempDirectory();
        Build(temp, allowInsecure: false).Classify("http://app.example.com").Should().Be(OriginClassification.Rejected);
    }

    [Fact]
    public void Classify_HttpOrigin_UnknownWhenAllowInsecureTrue()
    {
        using var temp = new TempDirectory();
        Build(temp, allowInsecure: true).Classify("http://app.example.com").Should().Be(OriginClassification.Unknown);
    }

    [Fact]
    public void Classify_HttpsOriginNotInWhitelist_ReturnsUnknown()
    {
        using var temp = new TempDirectory();
        Build(temp).Classify("https://app.example.com").Should().Be(OriginClassification.Unknown);
    }

    [Fact]
    public void Classify_HttpsOriginInWhitelist_ReturnsAllowed()
    {
        using var temp = new TempDirectory();
        var configStore = new ConfigStore(Path.Combine(temp.Path, "config.json"));
        configStore.AddAllowedOrigin("https://app.example.com");
        var service = new OriginAuthorizationService(configStore, false);

        service.Classify("https://app.example.com").Should().Be(OriginClassification.Allowed);
    }
}
