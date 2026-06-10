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

    // Relies on Windows sharing semantics: on Unix, deleting an open file succeeds.
    [FactWindowsOnly]
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
            Directory.Exists(paths.AppDataRoot).Should().BeTrue();
            File.Exists(paths.ConfigFile).Should().BeFalse();
            File.Exists(paths.PfxFile).Should().BeFalse();
            File.Exists(paths.PfxPasswordFile).Should().BeFalse();
            File.Exists(paths.SumatraPdfPath).Should().BeFalse();
            File.Exists(lockedLog).Should().BeTrue();
        }
    }

    // Relies on Windows directory link semantics (symlink/junction reparse points).
    [FactWindowsOnly]
    public void TryDeleteRoot_DirectoryLinkInRoot_DoesNotDeleteLinkTargetContents()
    {
        using var temp = new TempDirectory();
        var paths = CreateFakeLayout(temp);

        // Link target lives OUTSIDE the app data root: cleanup must never reach into it.
        var targetDirectory = System.IO.Path.Combine(temp.Path, "link-target");
        Directory.CreateDirectory(targetDirectory);
        var targetFile = System.IO.Path.Combine(targetDirectory, "keep.txt");
        File.WriteAllText(targetFile, "must survive");

        var linkPath = System.IO.Path.Combine(paths.AppDataRoot, "linked");
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Symlink creation needs SeCreateSymbolicLinkPrivilege or Developer Mode;
            // without it there is nothing to exercise here, so bail out silently.
            return;
        }

        try
        {
            // Read-only keeps the fast path (recursive Directory.Delete) from removing the
            // link entry itself, so the per-entry fallback is the one that meets the link.
            File.SetAttributes(linkPath, FileAttributes.Directory | FileAttributes.ReadOnly);

            // A locked file makes the fast path fail and forces the fallback to run.
            var lockedLog = System.IO.Path.Combine(paths.LogsDirectory, "locked.log");
            using (new FileStream(lockedLog, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var act = () => AppDataCleanup.TryDeleteRoot(paths);

                act.Should().NotThrow();
            }

            Directory.Exists(targetDirectory).Should().BeTrue("the link target itself must be left alone");
            File.Exists(targetFile).Should().BeTrue("cleanup must not traverse into the link target");
        }
        finally
        {
            // Let TempDirectory dispose cleanly.
            if (Directory.Exists(linkPath)) File.SetAttributes(linkPath, FileAttributes.Directory);
        }
    }
}
