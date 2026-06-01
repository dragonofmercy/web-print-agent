namespace PrintAgent.Updating;

/// <summary>UI seam for the updater. Implemented by TrayIconHost (toast + tray menu). Mirrors IPairingUi.</summary>
public interface IUpdateUi
{
    /// <summary>An update is staged and ready. Show a clickable "ready, click to restart" toast.</summary>
    void NotifyUpdateReady(string version);

    /// <summary>A manual check found nothing. Show a brief "up to date" toast.</summary>
    void NotifyUpToDate();

    /// <summary>The user asked to restart but a print job is running; the update will apply later.</summary>
    void NotifyBusyDeferred();
}
