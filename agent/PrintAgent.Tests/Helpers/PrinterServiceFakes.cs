using NSubstitute;
using PrintAgent.Printing;

namespace PrintAgent.Tests.Helpers;

internal static class PrinterServiceFakes
{
    public static IPrinterService With(params string[] names)
    {
        var svc = Substitute.For<IPrinterService>();
        svc.List().Returns(names.Select(n => new PrinterInfo(n, false, "Idle", Array.Empty<string>())).ToList());
        return svc;
    }
}
