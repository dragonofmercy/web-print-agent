using System.Text.Json;

namespace PrintAgent.Protocol;

public sealed class RpcRouter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly Dictionary<string, IRpcHandler> _handlers;

    public RpcRouter(IEnumerable<IRpcHandler> handlers)
    {
        _handlers = handlers.ToDictionary(h => h.Method, StringComparer.Ordinal);
    }

    public async Task<string> DispatchAsync(string rawJson, ConnectionContext connection, CancellationToken ct)
    {
        JsonRpcRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<JsonRpcRequest>(rawJson, JsonOptions);
        }
        catch (JsonException)
        {
            return Serialize(ErrorResponse(null, JsonRpcErrorCodes.InvalidRequest, "Invalid Request"));
        }

        if (request is null || string.IsNullOrEmpty(request.Method))
            return Serialize(ErrorResponse(request?.Id, JsonRpcErrorCodes.InvalidRequest, "Invalid Request"));

        if (!_handlers.TryGetValue(request.Method, out var handler))
            return Serialize(ErrorResponse(request.Id, JsonRpcErrorCodes.MethodNotFound, $"Method not found: {request.Method}"));

        if (handler.RequiresPairedConnection && !connection.IsPaired)
            return Serialize(ErrorResponse(request.Id, JsonRpcErrorCodes.OriginNotAuthorized, "OriginNotAuthorized"));

        try
        {
            var result = await handler.HandleAsync(request.Params, connection, ct);
            return Serialize(new JsonRpcResponse { Id = request.Id, Result = result });
        }
        catch (ArgumentException ex)
        {
            return Serialize(ErrorResponse(request.Id, JsonRpcErrorCodes.InvalidParams, ex.Message));
        }
        catch (RpcApplicationException rpcEx)
        {
            return Serialize(ErrorResponse(request.Id, rpcEx.Code, rpcEx.Message));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Connection is going away; let the WebSocket receive loop's
            // OperationCanceledException handling end the connection cleanly.
            throw;
        }
        catch (Exception ex)
        {
            Serilog.Log.Logger.Error(ex, "Unhandled RPC error in method {Method}", request.Method);
            return Serialize(ErrorResponse(request.Id, JsonRpcErrorCodes.InternalError, "Internal error"));
        }
    }

    public static string SerializeNotification(JsonRpcNotification notification)
        => JsonSerializer.Serialize(notification, JsonOptions);

    private static JsonRpcResponse ErrorResponse(JsonElement? id, int code, string message)
        => new() { Id = id, Error = new JsonRpcError { Code = code, Message = message } };

    private static string Serialize(JsonRpcResponse response)
        => JsonSerializer.Serialize(response, JsonOptions);
}

public sealed class RpcApplicationException : Exception
{
    public int Code { get; }
    public RpcApplicationException(int code, string message) : base(message) => Code = code;
}
