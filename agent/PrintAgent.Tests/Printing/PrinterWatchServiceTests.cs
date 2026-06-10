using FluentAssertions;
using PrintAgent.Printing;
using PrintAgent.Tests.Helpers;
using Serilog;

namespace PrintAgent.Tests.Printing;

public class PrinterWatchServiceTests
{
    [FactWindowsOnly]
    public void StartThenDispose_DoesNotThrow()
    {
        // We cannot simulate a real printer hot-plug in a unit test; coalescing is covered by
        // DebouncerTests and the broadcast by ConnectionRegistryTests. This is a smoke test that
        // the WMI watcher starts and disposes cleanly.
        var act = () =>
        {
            using var watcher = new PrinterWatchService(Log.Logger, TimeSpan.FromMilliseconds(50));
            watcher.Start();
        };

        act.Should().NotThrow();
    }
}
