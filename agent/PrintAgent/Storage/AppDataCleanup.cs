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
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
            catch (Exception ex) when (IsFileSystemAccessError(ex)) { /* locked: skip */ }
        }

        foreach (var subDirectory in subDirectories)
        {
            DeleteContentsBestEffort(subDirectory);
            try { Directory.Delete(subDirectory, recursive: false); }
            catch (Exception ex) when (IsFileSystemAccessError(ex)) { /* not empty: skip */ }
        }
    }

    private static bool IsFileSystemAccessError(Exception ex)
        => ex is IOException or UnauthorizedAccessException;
}
