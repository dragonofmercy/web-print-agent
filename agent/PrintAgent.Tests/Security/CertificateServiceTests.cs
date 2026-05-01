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
}
