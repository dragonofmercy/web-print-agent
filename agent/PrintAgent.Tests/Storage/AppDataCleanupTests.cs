using FluentAssertions;
using PrintAgent.Storage;
using PrintAgent.Tests.Helpers;
using Xunit;

namespace PrintAgent.Tests.Storage;

public class AppDataCleanupTests
{
    private static Paths CreateFakeLayout(TempDirectory temp)
    {
        var paths = new Paths(System.IO.Path.Combine(temp.Path, "PrintAgent"));
        paths.EnsureLayout();
        File.WriteAllText(paths.ConfigFile, "{}");
        File.WriteAllText(paths.PfxFile, "fake-pfx");
        File.WriteAllText(paths.PfxPasswordFile, "fake-password");
        File.WriteAllText(System.IO.Path.Combine(paths.LogsDirectory, "printagent-20260101.log"), "log line");
        File.WriteAllText(paths.SumatraPdfPath, "fake-exe");
        return paths;
    }

    [Fact]
    public void TryDeleteRoot_FullLayout_DeletesEverythingIncludingRoot()
    {
        using var temp = new TempDirectory();
        var paths = CreateFakeLayout(temp);

        var result = AppDataCleanup.TryDeleteRoot(paths);

        result.Should().BeTrue();
        Directory.Exists(paths.AppDataRoot).Should().BeFalse();
    }

    [Fact]
    public void TryDeleteRoot_MissingRoot_ReturnsTrue()
    {
        using var temp = new TempDirectory();
        var paths = new Paths(System.IO.Path.Combine(temp.Path, "does-not-exist"));

        var result = AppDataCleanup.TryDeleteRoot(paths);

        result.Should().BeTrue();
    }

    [Fact]
    public void TryDeleteRoot_LockedFile_DoesNotThrowAndDeletesTheRest()
    {
        using var temp = new TempDirectory();
        var paths = CreateFakeLayout(temp);
        var lockedLog = System.IO.Path.Combine(paths.LogsDirectory, "locked.log");

        using (var locker = new FileStream(lockedLog, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            locker.Write(new byte[] { 1, 2, 3 });
            locker.Flush();

            var act = () => AppDataCleanup.TryDeleteRoot(paths);

            var result = act.Should().NotThrow().Subject;
            result.Should().BeFalse("the locked file prevents removing the root");
            File.Exists(paths.ConfigFile).Should().BeFalse();
            File.Exists(paths.PfxFile).Should().BeFalse();
            File.Exists(paths.PfxPasswordFile).Should().BeFalse();
            File.Exists(paths.SumatraPdfPath).Should().BeFalse();
            File.Exists(lockedLog).Should().BeTrue();
        }
    }
}
