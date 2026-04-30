using System.Text.Json;

namespace PrintAgent.Protocol;

public interface IRpcHandler
{
    string Method { get; }
    bool RequiresPairedConnection { get; }
    Task<object?> HandleAsync(JsonElement? @params, ConnectionContext connection, CancellationToken cancellationToken);
}
