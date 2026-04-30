using System.Globalization;
using System.Resources;

namespace PrintAgent.Localization;

public static class Strings
{
    private static readonly ResourceManager _rm =
        new("PrintAgent.Localization.Strings", typeof(Strings).Assembly);

    private static string Get(string key)
        => _rm.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    private static string Format(string key, params object[] args)
        => string.Format(CultureInfo.CurrentUICulture, Get(key), args);

    public static string PairingTitle => Get("Pairing_Title");
    public static string PairingMessage(string origin) => Format("Pairing_Message", origin);
    public static string PairingAllow => Get("Pairing_Allow");
    public static string PairingRefuse => Get("Pairing_Refuse");

    public static string TrayStatus => Get("Tray_Status");
    public static string TrayStatusRunning(int port) => Format("Tray_StatusRunning", port);
    public static string TrayStatusStopped => Get("Tray_StatusStopped");
    public static string TrayOpenLogs => Get("Tray_OpenLogs");
    public static string TrayQuit => Get("Tray_Quit");

    public static string BootstrapFailedToStart(string error) => Format("Bootstrap_FailedToStart", error);

    public static string TrayAuthorizedOrigins => Get("Tray_AuthorizedOrigins");
    public static string OriginsTitle => Get("Origins_Title");
    public static string OriginsHeaderOrigin => Get("Origins_Header_Origin");
    public static string OriginsEmpty => Get("Origins_Empty");
    public static string OriginsRemoveSelected => Get("Origins_RemoveSelected");
    public static string OriginsRemoveAll => Get("Origins_RemoveAll");
    public static string OriginsRefresh => Get("Origins_Refresh");
    public static string OriginsClose => Get("Origins_Close");
    public static string OriginsConfirmRemove(int count) => Format("Origins_ConfirmRemove", count);
    public static string OriginsConfirmRemoveAll => Get("Origins_ConfirmRemoveAll");

    /// <summary>
    /// Selects the UI culture for the rest of the process.
    /// "auto" (default) keeps the OS-detected culture; otherwise an explicit
    /// IETF tag like "en", "fr", "fr-FR" forces that culture.
    /// </summary>
    public static void ApplyCulture(string language)
    {
        if (string.IsNullOrWhiteSpace(language) || language.Equals("auto", StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            var culture = new CultureInfo(language);
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
        }
        catch (CultureNotFoundException)
        {
            // Fall back to OS default; nothing else to do.
        }
    }
}
