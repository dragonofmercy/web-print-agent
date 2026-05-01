using System.Diagnostics;
using FluentAssertions;
using PrintAgent.Printing;
using PrintAgent.Tests.Helpers;
using Xunit;

namespace PrintAgent.Tests.Printing;

public class SumatraPdfTimeoutTests
{
    [FactWindowsOnly]
    public async Task RunAsync_BinaryHangsBeyondTimeout_KillsProcessAndReportsTimeout()
    {
        var cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        var sw = Stopwatch.StartNew();

        var psi = new ProcessStartInfo
        {
            FileName = cmd,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("/c");
        psi.ArgumentList.Add("ping -n 30 127.0.0.1 > NUL");

        var result = await SumatraPdfRunner.RunWithTimeoutAsync(
            psi, TimeSpan.FromSeconds(1), CancellationToken.None);

        sw.Stop();
        sw.Elapsed.TotalSeconds.Should().BeLessThan(5);
        result.ExitCode.Should().NotBe(0);
        result.StandardError.Should().Contain("timed out");
    }
}
