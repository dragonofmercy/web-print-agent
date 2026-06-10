namespace PrintAgent.Storage;

/// <summary>
/// Best-effort removal of the whole app data root (%APPDATA%\PrintAgent) on uninstall.
/// A locked file (e.g. a log still held by a dying process) never throws out of here;
/// everything else is still deleted and the locked entries are left behind.
/// </summary>
public static class AppDataCleanup
{
    /// <returns>true when the root directory is fully gone; false if anything was left behind.</returns>
    public static bool TryDeleteRoot(Paths paths)
    {
        var root = paths.AppDataRoot;
        if (!Directory.Exists(root)) return true;

        // Fast path: one recursive delete.
        try
        {
            Directory.Delete(root, recursive: true);
            return true;
        }
        catch (Exception ex) when (IsFileSystemAccessError(ex))
        {
            // A locked or protected entry aborted the recursive delete partway.
            // Fall back to per-entry deletion so the rest still gets removed.
        }

        DeleteContentsBestEffort(root);
        try
        {
            Directory.Delete(root, recursive: false);
            return true;
        }
        catch (Exception ex) when (IsFileSystemAccessError(ex))
        {
            return false;
        }
    }

    private static void DeleteContentsBestEffort(string directory)
    {
        string[] files, subDirectories;
        try
        {
            files = Directory.GetFiles(directory);
            subDirectories = Directory.GetDirectories(directory);
        }
        catch (Exception ex) when (IsFileSystemAccessError(ex))
        {
            return;
        }

        foreach (var file in files)
        {
            try
            {
                // For a file reparse point (symlink), delete the link itself without touching
                // attributes, so the read-only flag of the link target is never cleared.
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0)
                    File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
            catch (Exception ex) when (IsFileSystemAccessError(ex)) { /* locked: skip */ }
        }

        foreach (var subDirectory in subDirectories)
        {
            try
            {
                // The fast path (recursive Directory.Delete) does not traverse reparse points
                // (junctions/symlinks) either: delete the link itself, never its target's contents.
                if ((File.GetAttributes(subDirectory) & FileAttributes.ReparsePoint) == 0)
                    DeleteContentsBestEffort(subDirectory);
                Directory.Delete(subDirectory, recursive: false);
            }
            catch (Exception ex) when (IsFileSystemAccessError(ex)) { /* not empty: skip */ }
        }
    }

    private static bool IsFileSystemAccessError(Exception ex)
        => ex is IOException or UnauthorizedAccessException;
}
