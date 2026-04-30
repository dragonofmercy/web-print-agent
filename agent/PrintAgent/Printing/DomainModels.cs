namespace PrintAgent.Printing;

public sealed record PrinterInfo(
    string Name,
    bool IsDefault,
    string Status,
    string[] PaperSizes,
    string[] PaperSources);

public sealed record PrintOptions(
    int Copies = 1,
    string? PaperSize = null,
    bool Color = true,
    PrintOrientation Orientation = PrintOrientation.Default,
    string? Tray = null);

public enum PrintOrientation { Default, Portrait, Landscape }

public enum JobStatus { Submitted, Printing, Completed, Failed }

public sealed record JobEvent(Guid JobId, JobStatus Status, string? Error = null);

public sealed record PrintJob(
    Guid JobId,
    string PrinterName,
    string PdfPath,
    PrintOptions Options,
    Guid SubmittingConnectionId);
