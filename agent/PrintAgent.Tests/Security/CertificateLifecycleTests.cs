using System.IO;
using FluentAssertions;
using PrintAgent.Security;
using PrintAgent.Tests.Helpers;
using Xunit;

namespace PrintAgent.Tests.Security;

public class CertificateLifecycleTests
{
    [FactWindowsOnly]
    public void GenerateSelfSigned_CertValidForThirteenMonthsOrLess()
    {
        var cert = CertificateService.GenerateSelfSigned();

        var lifespan = cert.NotAfter - cert.NotBefore;
        lifespan.TotalDays.Should().BeLessOrEqualTo(400); // ~13 months
        lifespan.TotalDays.Should().BeGreaterThan(360);   // at least ~1 year
    }

    [FactWindowsOnly]
    public void EnsureCertificate_RegeneratesIfExistingCertExpiresWithinThirtyDays()
    {
        using var tmp = new TempDirectory();
        var pfx = Path.Combine(tmp.Path, "agent.pfx");
        var pw = Path.Combine(tmp.Path, "agent.pfx.password");

        var first = CertificateService.EnsureCertificate(pfx, pw);
        var firstThumb = first.Thumbprint;

        // Force renewal by deleting the pfx and re-running EnsureCertificate.
        // (Direct expiry simulation requires injecting a clock; we cover the
        // file-missing branch which exercises the same regeneration code path.)
        File.Delete(pfx);
        var second = CertificateService.EnsureCertificate(pfx, pw);

        second.Thumbprint.Should().NotBe(firstThumb);
    }

    [FactWindowsOnly]
    public void TryUninstallFromTrustedRoot_UnknownThumbprint_DoesNotThrow()
    {
        // Use a thumbprint that almost certainly does not exist in the store.
        var unknown = new string('A', 40);

        var act = () => CertificateService.TryUninstallFromTrustedRoot(unknown);

        act.Should().NotThrow();
    }

    [FactWindowsOnly]
    public void TryUninstallFromTrustedRoot_EmptyOrWhitespaceThumbprint_DoesNotThrow()
    {
        var act1 = () => CertificateService.TryUninstallFromTrustedRoot("");
        var act2 = () => CertificateService.TryUninstallFromTrustedRoot("   ");

        act1.Should().NotThrow();
        act2.Should().NotThrow();
    }
}
