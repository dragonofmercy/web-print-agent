namespace PrintAgent.Protocol;

public sealed class ConnectionContext
{
    public Guid ConnectionId { get; } = Guid.NewGuid();
    public string Origin { get; init; } = string.Empty;
    public bool IsPaired { get; set; }
    public Func<JsonRpcNotification, CancellationToken, Task> SendNotificationAsync { get; init; } = (_, _) => Task.CompletedTask;
}
