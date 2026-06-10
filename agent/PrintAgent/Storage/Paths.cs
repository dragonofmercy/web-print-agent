using System.Security.AccessControl;
using System.Security.Principal;

namespace PrintAgent.Storage;

public sealed class Paths
{
    public string AppDataRoot { get; }
    public string ConfigFile => Path.Combine(AppDataRoot, "config.json");
    public string PfxFile => Path.Combine(AppDataRoot, "printagent.pfx");
    public string PfxPasswordFile => Path.Combine(AppDataRoot, "printagent.pfx.password");
    public string LogsDirectory => Path.Combine(AppDataRoot, "logs");
    public string BinDirectory => Path.Combine(AppDataRoot, "bin");
    public string SumatraPdfPath => Path.Combine(BinDirectory, "SumatraPDF.exe");
    public string TempPdfPattern => "printagent-*.pdf";

    /// <summary>
    /// Set when <see cref="EnsureLayout"/> could not apply the restrictive ACL.
    /// EnsureLayout runs before Serilog is configured, so the warning is recorded
    /// here and logged by the caller once a logger exists.
    /// </summary>
    public string? AclWarning { get; private set; }

    public Paths(string? appDataOverride = null)
    {
        AppDataRoot = appDataOverride ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PrintAgent");
    }

    public void EnsureLayout()
    {
        Directory.CreateDirectory(AppDataRoot);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(BinDirectory);
        ApplyRestrictiveAcl();
    }

    /// <summary>
    /// Locks down the app data root (config.json, printagent.pfx, printagent.pfx.password,
    /// bin/, logs/) with a protected DACL that child files and subdirectories inherit.
    /// </summary>
    private void ApplyRestrictiveAcl()
    {
        // The app is Windows-only, but Paths is also exercised by tests that may run elsewhere.
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var currentUser = identity.User
                ?? throw new InvalidOperationException("Current Windows identity has no SID.");
            var localSystem = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            // Administrators keep FullControl: standard practice, since admins can take
            // ownership of any object anyway; excluding them only breaks support/backup
            // scenarios without adding real security. Well-known SIDs are used throughout
            // instead of account names, which are localized (e.g. "Systeme" on French Windows).
            var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);

            const InheritanceFlags inheritToChildren = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;

            // A fresh DirectorySecurity with SetAccessControl replaces the whole DACL,
            // so re-running EnsureLayout is idempotent (no rule accumulation).
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(currentUser, FileSystemRights.FullControl, inheritToChildren, PropagationFlags.None, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(localSystem, FileSystemRights.FullControl, inheritToChildren, PropagationFlags.None, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(administrators, FileSystemRights.FullControl, inheritToChildren, PropagationFlags.None, AccessControlType.Allow));

            new DirectoryInfo(AppDataRoot).SetAccessControl(security);
            AclWarning = null;
        }
        catch (Exception ex)
        {
            // The ACL is defense in depth (the PFX password is DPAPI-protected regardless),
            // so failing startup on filesystems without ACL support (e.g. FAT32 roaming
            // profiles) would hurt more than it protects. Record the warning for the caller.
            AclWarning = $"Could not apply restrictive ACL to '{AppDataRoot}': {ex.Message}";
        }
    }
}
