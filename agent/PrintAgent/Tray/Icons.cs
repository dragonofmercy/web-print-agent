using System.Drawing;
using System.Reflection;

namespace PrintAgent.Tray;

internal static class Icons
{
    /// <summary>Loads the embedded icon.ico with all its sizes (Windows picks the best one for context).</summary>
    public static Icon? LoadFull()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("icon.ico");
            return stream is null ? null : new Icon(stream);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Loads the embedded icon.ico extracted at the requested size (e.g. tray's SmallIconSize).</summary>
    public static Icon? LoadAt(Size size)
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("icon.ico");
            return stream is null ? null : new Icon(stream, size);
        }
        catch
        {
            return null;
        }
    }
}
