using System.Text.Json;
using PrintAgent;
using PrintAgent.Security;

namespace PrintAgent.Protocol.Handlers;

public sealed class AgentHelloHandler : IRpcHandler
{
    public string Method => "agent.hello";
    public bool RequiresPairedConnection => false;

    private readonly IPairingCoordinator _pairing;

    public AgentHelloHandler(IPairingCoordinator pairing) => _pairing = pairing;

    public async Task<object?> HandleAsync(JsonElement? @params, ConnectionContext connection, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(connection.Origin))
            throw new RpcApplicationException(JsonRpcErrorCodes.OriginNotAuthorized, "Missing origin.");

        var decision = await _pairing.RequestApprovalAsync(connection.Origin, ct);
        if (decision != PairingDecision.Approved)
            throw new RpcApplicationException(JsonRpcErrorCodes.OriginNotAuthorized, "OriginNotAuthorized");

        connection.IsPaired = true;

        return new
        {
            agentVersion = AppInfo.Version,
            capabilities = new[] { "getLocalPrinters", "print", "getJobStatus" },
            jobEventsSupported = true
        };
    }
}
