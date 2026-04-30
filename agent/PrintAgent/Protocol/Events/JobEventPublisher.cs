using PrintAgent.Printing;

namespace PrintAgent.Protocol.Events;

public sealed class JobEventPublisher
{
    public Func<Guid, JobEvent, CancellationToken, Task> SendAsync { get; set; }
        = (_, _, _) => Task.CompletedTask;

    public Task PublishAsync(Guid connectionId, JobEvent ev, CancellationToken ct)
        => SendAsync(connectionId, ev, ct);
}
