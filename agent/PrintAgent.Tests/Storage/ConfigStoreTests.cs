using FluentAssertions;
using PrintAgent.Storage;
using PrintAgent.Tests.Helpers;
using Xunit;

namespace PrintAgent.Tests.Storage;

public class ConfigStoreTests
{
    [Fact]
    public void Load_FreshDirectory_ReturnsEmptyConfig()
    {
        using var temp = new TempDirectory();
        var store = new ConfigStore(System.IO.Path.Combine(temp.Path, "config.json"));

        var config = store.Load();

        config.AllowedOrigins.Should().BeEmpty();
        config.LastBoundPort.Should().BeNull();
    }

    [Fact]
    public void AddAllowedOrigin_NewOrigin_PersistsAndReturnsTrue()
    {
        using var temp = new TempDirectory();
        var path = System.IO.Path.Combine(temp.Path, "config.json");
        var store = new ConfigStore(path);

        var added = store.AddAllowedOrigin("https://app.example.com");

        added.Should().BeTrue();
        var reloaded = new ConfigStore(path).Load();
        reloaded.AllowedOrigins.Should().ContainSingle().Which.Should().Be("https://app.example.com");
    }

    [Fact]
    public void AddAllowedOrigin_DuplicateOrigin_ReturnsFalse()
    {
        using var temp = new TempDirectory();
        var store = new ConfigStore(System.IO.Path.Combine(temp.Path, "config.json"));
        store.AddAllowedOrigin("https://app.example.com");

        var addedAgain = store.AddAllowedOrigin("https://app.example.com");

        addedAgain.Should().BeFalse();
    }

    [Fact]
    public void IsOriginAllowed_AfterAdd_ReturnsTrue()
    {
        using var temp = new TempDirectory();
        var store = new ConfigStore(System.IO.Path.Combine(temp.Path, "config.json"));
        store.AddAllowedOrigin("https://app.example.com");

        store.IsOriginAllowed("https://app.example.com").Should().BeTrue();
        store.IsOriginAllowed("https://other.example.com").Should().BeFalse();
    }

    [Fact]
    public void SetLastBoundPort_PersistsAcrossInstances()
    {
        using var temp = new TempDirectory();
        var path = System.IO.Path.Combine(temp.Path, "config.json");
        new ConfigStore(path).SetLastBoundPort(8444);

        new ConfigStore(path).Load().LastBoundPort.Should().Be(8444);
    }
}
