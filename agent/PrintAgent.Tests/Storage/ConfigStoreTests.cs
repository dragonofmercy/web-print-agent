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

    [Fact]
    public void GetAllowedOrigins_ReturnsSnapshotInsertionOrder()
    {
        using var temp = new TempDirectory();
        var store = new ConfigStore(System.IO.Path.Combine(temp.Path, "config.json"));
        store.AddAllowedOrigin("https://a.test");
        store.AddAllowedOrigin("https://b.test");
        store.AddAllowedOrigin("https://c.test");

        var origins = store.GetAllowedOrigins();

        origins.Should().Equal("https://a.test", "https://b.test", "https://c.test");
    }

    [Fact]
    public void RemoveAllowedOrigin_KnownOrigin_RemovesAndReturnsTrue()
    {
        using var temp = new TempDirectory();
        var path = System.IO.Path.Combine(temp.Path, "config.json");
        var store = new ConfigStore(path);
        store.AddAllowedOrigin("https://a.test");
        store.AddAllowedOrigin("https://b.test");

        var removed = store.RemoveAllowedOrigin("https://a.test");

        removed.Should().BeTrue();
        new ConfigStore(path).GetAllowedOrigins().Should().Equal("https://b.test");
    }

    [Fact]
    public void RemoveAllowedOrigin_UnknownOrigin_ReturnsFalse()
    {
        using var temp = new TempDirectory();
        var store = new ConfigStore(System.IO.Path.Combine(temp.Path, "config.json"));
        store.AddAllowedOrigin("https://a.test");

        store.RemoveAllowedOrigin("https://nope.test").Should().BeFalse();
        store.GetAllowedOrigins().Should().Equal("https://a.test");
    }

    [Fact]
    public void RemoveAllowedOrigins_BatchMixedKnownUnknown_RemovesKnownOnlyAndReturnsCount()
    {
        using var temp = new TempDirectory();
        var path = System.IO.Path.Combine(temp.Path, "config.json");
        var store = new ConfigStore(path);
        store.AddAllowedOrigin("https://a.test");
        store.AddAllowedOrigin("https://b.test");
        store.AddAllowedOrigin("https://c.test");

        var removed = store.RemoveAllowedOrigins(new[] { "https://a.test", "https://nope.test", "https://c.test" });

        removed.Should().Be(2);
        new ConfigStore(path).GetAllowedOrigins().Should().Equal("https://b.test");
    }

    [Fact]
    public void RemoveAllowedOrigins_NoneMatch_DoesNotPersist()
    {
        using var temp = new TempDirectory();
        var path = System.IO.Path.Combine(temp.Path, "config.json");
        var store = new ConfigStore(path);
        store.AddAllowedOrigin("https://a.test");
        var lastWrite = System.IO.File.GetLastWriteTimeUtc(path);

        Thread.Sleep(50);
        var removed = store.RemoveAllowedOrigins(new[] { "https://nope.test", "https://other.test" });

        removed.Should().Be(0);
        System.IO.File.GetLastWriteTimeUtc(path).Should().Be(lastWrite);
    }

    [Fact]
    public void ClearAllowedOrigins_ReturnsRemovedCountAndPersists()
    {
        using var temp = new TempDirectory();
        var path = System.IO.Path.Combine(temp.Path, "config.json");
        var store = new ConfigStore(path);
        store.AddAllowedOrigin("https://a.test");
        store.AddAllowedOrigin("https://b.test");

        var count = store.ClearAllowedOrigins();

        count.Should().Be(2);
        new ConfigStore(path).GetAllowedOrigins().Should().BeEmpty();
    }

    [Fact]
    public void ClearAllowedOrigins_OnEmpty_ReturnsZero()
    {
        using var temp = new TempDirectory();
        var store = new ConfigStore(System.IO.Path.Combine(temp.Path, "config.json"));

        store.ClearAllowedOrigins().Should().Be(0);
    }

    [Fact]
    public void ConfigModel_AutoUpdate_DefaultsToTrue()
    {
        new ConfigModel().AutoUpdate.Should().BeTrue();
    }

    [Fact]
    public void Load_PreservesAutoUpdateFalse_AfterRoundTrip()
    {
        using var temp = new TempDirectory();
        var path = System.IO.Path.Combine(temp.Path, "config.json");
        System.IO.File.WriteAllText(path, "{\"AutoUpdate\":false}");

        var store = new ConfigStore(path);

        store.Load().AutoUpdate.Should().BeFalse();
    }
}
