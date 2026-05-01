using System.Reflection;

namespace PrintAgent;

public static class AppInfo
{
    /// <summary>
    /// SemVer-style version string (e.g. "0.1.0") sourced from
    /// AssemblyInformationalVersionAttribute, with any "+commit-sha" suffix removed.
    /// Falls back to AssemblyVersion or "0.0.0" if unavailable.
    /// </summary>
    public static string Version { get; } = ResolveVersion();

    private static string ResolveVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(info))
        {
            var plus = info.IndexOf('+');
            return plus > 0 ? info.Substring(0, plus) : info;
        }
        return asm.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
