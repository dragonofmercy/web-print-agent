using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using PrintAgent.Security;
using PrintAgent.Tests.Helpers;
using Xunit;

namespace PrintAgent.Tests.Security;

public class CertificateServiceTests
{
    [Fact]
    public void GenerateSelfSigned_ReturnsCertWithLocalhostCnAnd127SubjectAlternativeNames()
    {
        var cert = CertificateService.GenerateSelfSigned();

        cert.Subject.Should().Contain("CN=localhost");
        var sanExtension = cert.Extensions.OfType<X509Extension>()
            .FirstOrDefault(e => e.Oid?.Value == "2.5.29.17");
        sanExtension.Should().NotBeNull();
        sanExtension!.Format(true).Should().Contain("localhost").And.Contain("127.0.0.1");
    }

    [Fact]
    public void EnsureCertificate_FreshState_GeneratesPfxAndPasswordFiles()
    {
        using var temp = new TempDirectory();
        var pfxPath = Path.Combine(temp.Path, "p.pfx");
        var pwdPath = Path.Combine(temp.Path, "p.pfx.password");

        var cert = CertificateService.EnsureCertificate(pfxPath, pwdPath);

        File.Exists(pfxPath).Should().BeTrue();
        File.Exists(pwdPath).Should().BeTrue();
        cert.Should().NotBeNull();
    }

    [Fact]
    public void EnsureCertificate_ExistingPfx_ReturnsLoadedSameCert()
    {
        using var temp = new TempDirectory();
        var pfxPath = Path.Combine(temp.Path, "p.pfx");
        var pwdPath = Path.Combine(temp.Path, "p.pfx.password");

        var first = CertificateService.EnsureCertificate(pfxPath, pwdPath);
        var second = CertificateService.EnsureCertificate(pfxPath, pwdPath);

        second.Thumbprint.Should().Be(first.Thumbprint);
    }

    [FactWindowsOnly]
    public void EnsureCertificate_CorruptedPasswordFile_RegeneratesInsteadOfThrowing()
    {
        using var temp = new TempDirectory();
        var pfxPath = Path.Combine(temp.Path, "p.pfx");
        var pwdPath = Path.Combine(temp.Path, "p.pfx.password");

        var first = CertificateService.EnsureCertificate(pfxPath, pwdPath);
        var corruptedBytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0x42 };
        File.WriteAllBytes(pwdPath, corruptedBytes);

        var regenerated = CertificateService.EnsureCertificate(pfxPath, pwdPath);

        regenerated.Should().NotBeNull();
        regenerated.Thumbprint.Should().NotBe(first.Thumbprint);
        regenerated.NotAfter.Should().BeAfter(DateTime.UtcNow.AddDays(30));
        File.ReadAllBytes(pwdPath).Should().NotEqual(corruptedBytes, "the stale password file must be overwritten");

        // The rewritten pfx/password pair must be loadable on the next run.
        var reloaded = CertificateService.EnsureCertificate(pfxPath, pwdPath);
        reloaded.Thumbprint.Should().Be(regenerated.Thumbprint);
    }
}
