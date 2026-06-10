using System.Security.AccessControl;
using System.Security.Principal;
using FluentAssertions;
using PrintAgent.Storage;
using PrintAgent.Tests.Helpers;
using Xunit;

namespace PrintAgent.Tests.Storage;

public class PathsAclTests
{
    private static readonly SecurityIdentifier CurrentUserSid = WindowsIdentity.GetCurrent().User!;
    private static readonly SecurityIdentifier SystemSid = new(WellKnownSidType.LocalSystemSid, null);
    private static readonly SecurityIdentifier AdministratorsSid = new(WellKnownSidType.BuiltinAdministratorsSid, null);

    private static Paths CreateLayout(TempDirectory temp)
    {
        var paths = new Paths(System.IO.Path.Combine(temp.Path, "PrintAgent"));
        paths.EnsureLayout();
        return paths;
    }

    [FactWindowsOnly]
    public void EnsureLayout_DisablesInheritanceAndGrantsFullControlToExpectedSidsOnly()
    {
        using var temp = new TempDirectory();
        var paths = CreateLayout(temp);

        var security = new DirectoryInfo(paths.AppDataRoot).GetAccessControl();

        security.AreAccessRulesProtected.Should().BeTrue("inheritance from the parent directory must be disabled");

        var rules = security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>()
            .ToList();

        rules.Select(r => (SecurityIdentifier)r.IdentityReference)
            .Should().BeEquivalentTo(new[] { CurrentUserSid, SystemSid, AdministratorsSid });

        rules.Should().OnlyContain(r =>
            r.AccessControlType == AccessControlType.Allow &&
            r.FileSystemRights == FileSystemRights.FullControl &&
            r.InheritanceFlags == (InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit) &&
            r.PropagationFlags == PropagationFlags.None);

        paths.AclWarning.Should().BeNull();
    }

    [FactWindowsOnly]
    public void EnsureLayout_ChildFileAndSubdirectoryInheritTheAcl()
    {
        using var temp = new TempDirectory();
        var paths = CreateLayout(temp);

        File.WriteAllText(paths.ConfigFile, "{}");

        var fileRules = new FileInfo(paths.ConfigFile).GetAccessControl()
            .GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>()
            .ToList();

        fileRules.Should().OnlyContain(r => r.IsInherited, "the file must get its rules from the root directory");
        fileRules.Select(r => (SecurityIdentifier)r.IdentityReference)
            .Should().BeEquivalentTo(new[] { CurrentUserSid, SystemSid, AdministratorsSid });
        fileRules.Should().OnlyContain(r => r.FileSystemRights == FileSystemRights.FullControl && r.AccessControlType == AccessControlType.Allow);

        var subdirectoryRules = new DirectoryInfo(paths.LogsDirectory).GetAccessControl()
            .GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>()
            .ToList();

        subdirectoryRules.Should().OnlyContain(r => r.IsInherited, "subdirectories must get their rules from the root directory");
        subdirectoryRules.Select(r => (SecurityIdentifier)r.IdentityReference)
            .Should().BeEquivalentTo(new[] { CurrentUserSid, SystemSid, AdministratorsSid });
    }

    [FactWindowsOnly]
    public void EnsureLayout_CalledTwice_SucceedsWithoutDuplicatingRules()
    {
        using var temp = new TempDirectory();
        var paths = CreateLayout(temp);

        var act = () => paths.EnsureLayout();
        act.Should().NotThrow();

        var rules = new DirectoryInfo(paths.AppDataRoot).GetAccessControl()
            .GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>()
            .ToList();

        rules.Should().HaveCount(3, "re-applying the ACL must replace the rules, not accumulate them");
        paths.AclWarning.Should().BeNull();
    }
}
