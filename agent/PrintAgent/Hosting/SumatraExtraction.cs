using System.Reflection;
using System.Security.Cryptography;

namespace PrintAgent;

public static class SumatraExtraction
{
    public static bool FileMatchesSha256(string path, byte[] expectedHash)
    {
        if (!File.Exists(path)) return false;
        using var stream = File.OpenRead(path);
        var actual = SHA256.HashData(stream);
        return CryptographicOperations.FixedTimeEquals(actual, expectedHash);
    }

    public static bool TryExtract(string targetPath, out string? warning)
    {
        warning = null;
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SumatraPDF.exe");
        if (stream is null)
        {
            warning = $"SumatraPDF.exe not embedded; PDF printing will fail until the binary is placed at {targetPath}.";
            return false;
        }

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var bytes = ms.ToArray();
        var expected = SHA256.HashData(bytes);

        if (FileMatchesSha256(targetPath, expected)) return true;

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.WriteAllBytes(targetPath, bytes);
        return true;
    }
}
