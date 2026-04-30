using System.Runtime.InteropServices;
using Xunit;

namespace PrintAgent.Tests.Helpers;

public sealed class FactWindowsOnlyAttribute : FactAttribute
{
    public FactWindowsOnlyAttribute()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Skip = "Windows-only test.";
    }
}
