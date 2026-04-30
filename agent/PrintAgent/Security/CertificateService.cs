using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace PrintAgent.Security;

[SupportedOSPlatform("windows")]
public static class CertificateService
{
    public static X509Certificate2 GenerateSelfSigned()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=localhost",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, false));

        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
            critical: true));

        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new("1.3.6.1.5.5.7.3.1") }, critical: false));

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddIpAddress(System.Net.IPAddress.Loopback);
        request.CertificateExtensions.Add(sanBuilder.Build());

        var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(10));

        // Re-import so the private key is exportable on disk
        return new X509Certificate2(
            cert.Export(X509ContentType.Pfx),
            (string?)null,
            X509KeyStorageFlags.Exportable);
    }

    public static X509Certificate2 EnsureCertificate(string pfxPath, string passwordFilePath)
    {
        if (File.Exists(pfxPath) && File.Exists(passwordFilePath))
        {
            var password = ReadPassword(passwordFilePath);
            try
            {
                return new X509Certificate2(
                    pfxPath, password,
                    X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
            }
            catch (CryptographicException) { /* fall through and regenerate */ }
        }

        var newPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var cert = GenerateSelfSigned();
        File.WriteAllBytes(pfxPath, cert.Export(X509ContentType.Pfx, newPassword));
        WritePassword(passwordFilePath, newPassword);
        return new X509Certificate2(
            pfxPath, newPassword,
            X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
    }

    public static void TryInstallToTrustedRoot(X509Certificate2 cert)
    {
        try
        {
            using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            if (!store.Certificates.Find(X509FindType.FindByThumbprint, cert.Thumbprint, false).Any())
            {
                store.Add(cert);
            }
            store.Close();
        }
        catch (CryptographicException)
        {
            // User refused the UAC trust prompt -- we continue, browser will warn.
        }
    }

    private static void WritePassword(string path, string password)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(password);
        var protectedBytes = ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(path, protectedBytes);
    }

    private static string ReadPassword(string path)
    {
        var protectedBytes = File.ReadAllBytes(path);
        var bytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }
}
