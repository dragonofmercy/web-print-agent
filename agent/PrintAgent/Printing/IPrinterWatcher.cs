namespace PrintAgent.Printing;

/// <summary>
/// Watches the machine for printer add/remove events and raises <see cref="Changed"/>
/// (coalesced) so callers can notify clients to re-query the printer list.
/// </summary>
public interface IPrinterWatcher : IDisposable
{
    event EventHandler? Changed;

    void Start();
}
