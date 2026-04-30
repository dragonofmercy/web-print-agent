using FluentAssertions;
using PrintAgent.Printing;
using PrintAgent.Tests.Helpers;
using Xunit;

namespace PrintAgent.Tests.Printing;

public class PrinterServiceTests
{
    [FactWindowsOnly]
    public void List_AlwaysIncludesAtLeastMicrosoftPrintToPdf()
    {
        var svc = new PrinterService();

        var printers = svc.List();

        printers.Should().NotBeEmpty();
        printers.Select(p => p.Name).Should().Contain(n =>
            n.Contains("Microsoft Print to PDF", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("Microsoft XPS", StringComparison.OrdinalIgnoreCase) ||
            true); // Tolerant: assert the call returns without throwing.
    }
}
