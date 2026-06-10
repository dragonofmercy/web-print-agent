using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Http;
using PrintAgent.Printing;
using PrintAgent.Protocol;
using PrintAgent.Protocol.Events;
using PrintAgent.Security;
using Serilog;

namespace PrintAgent.Hosting;

public sealed class WebSocketEndpoint
{
    private readonly RpcRouter _router;
    private readonly OriginAuthorizationService _origins;
    private readonly JobEventPublisher _publisher;
    private readonly ILogger _log;
    private readonly int _maxMessageBytes;
    private readonly int _maxActiveConnections;
    private readonly ConnectionRegistry _connections;
    private readonly ConcurrentDictionary<Guid, WebSocket> _activeSockets = new();

    public WebSocketEndpoint(
        RpcRouter router,
        OriginAuthorizationService origins,
        JobEventPublisher publisher,
        ILogger log,
        int maxMessageBytes,
        int maxActiveConnections,
        ConnectionRegistry connections)
    {
        _router = router;
        _origins = origins;
        _publisher = publisher;
        _log = log;
        _maxMessageBytes = maxMessageBytes;
        _maxActiveConnections = maxActiveConnections;
        _connections = connections;
    }

    public async Task HandleAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var origin = context.Request.Headers.Origin.ToString();
        var classification = _origins.Classify(origin);
        if (classification == OriginClassification.Rejected)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        if (_activeSockets.Count >= _maxActiveConnections)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        using var ws = await context.WebSockets.AcceptWebSocketAsync();
        using var sendGate = new SendGate();

        var normalizedOrigin = _origins.Normalize(origin);
        var connection = new ConnectionContext
        {
            Origin = normalizedOrigin,
            IsPaired = classification == OriginClassification.Allowed,
            SendNotificationAsync = (n, ct) => SendAsync(ws, sendGate, RpcRouter.SerializeNotification(n), ct)
        };

        try
        {
            _activeSockets[connection.ConnectionId] = ws;
            _connections.Add(connection);

            using var subscription = _publisher.Subscribe(connection.ConnectionId, (ev, ct) =>
            {
                var notif = new JsonRpcNotification
                {
                    Method = "job.statusChanged",
                    Params = new { jobId = ev.JobId.ToString(), status = ev.Status.ToString(), error = ev.Error }
                };
                return SendAsync(ws, sendGate, RpcRouter.SerializeNotification(notif), ct);
            });

            await ReceiveLoopAsync(ws, sendGate, connection, context.RequestAborted);
        }
        catch (OperationCanceledException) { /* normal during shutdown */ }
        catch (WebSocketException) { /* normal when peer disconnects */ }
        catch (Exception ex) { _log.Warning(ex, "WS loop error for origin {Origin}", normalizedOrigin); }
        finally
        {
            _activeSockets.TryRemove(connection.ConnectionId, out _);
            _connections.Remove(connection.ConnectionId);
        }
    }

    public async Task CloseAllAsync(CancellationToken ct)
    {
        var sockets = _activeSockets.Values.ToList();
        if (sockets.Count == 0) return;

        _log.Information("Closing {Count} active WebSocket connection(s)...", sockets.Count);
        var tasks = sockets.Select(async ws =>
        {
            try
            {
                if (ws.State == WebSocketState.Open)
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "shutting down", ct);
            }
            catch { /* best effort */ }
        });
        await Task.WhenAll(tasks);
    }

    private async Task ReceiveLoopAsync(WebSocket ws, SendGate sendGate, ConnectionContext conn, CancellationToken ct)
    {
        var buffer = new byte[8192];
        while (ws.State == WebSocketState.Open)
        {
            using var ms = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, ct);
                    return;
                }
                ms.Write(buffer, 0, result.Count);
                if (ms.Length > _maxMessageBytes)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.MessageTooBig, "message too big", ct);
                    return;
                }
            } while (!result.EndOfMessage);

            var raw = Encoding.UTF8.GetString(ms.ToArray());
            var response = await _router.DispatchAsync(raw, conn, ct);
            await SendAsync(ws, sendGate, response, ct);
        }
    }

    private static Task SendAsync(WebSocket ws, SendGate sendGate, string text, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return sendGate.RunAsync(() => ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct), ct);
    }
}
