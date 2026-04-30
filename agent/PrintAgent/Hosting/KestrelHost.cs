using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace PrintAgent.Hosting;

public sealed class KestrelHost
{
    public int? BoundPort { get; private set; }
    private WebApplication? _app;

    public async Task StartAsync(
        int[] portRange,
        X509Certificate2 cert,
        WebSocketEndpoint endpoint,
        Serilog.ILogger logger,
        CancellationToken ct)
    {
        Exception? lastException = null;

        foreach (var port in portRange)
        {
            try
            {
                var builder = WebApplication.CreateBuilder();
                builder.Logging.ClearProviders();
                builder.Host.UseSerilog(logger);

                builder.WebHost.ConfigureKestrel(opts =>
                {
                    opts.Listen(IPAddress.Loopback, port, listen =>
                    {
                        listen.UseHttps(cert);
                        listen.Protocols = HttpProtocols.Http1AndHttp2;
                    });
                });

                var app = builder.Build();
                app.UseWebSockets(new WebSocketOptions
                {
                    KeepAliveInterval = TimeSpan.FromSeconds(30)
                });
                app.Map("/ws", endpoint.HandleAsync);
                app.MapGet("/", () => "PrintAgent");

                await app.StartAsync(ct);
                _app = app;

                // Resolve the actual bound port (matters when port == 0).
                if (port != 0)
                {
                    BoundPort = port;
                }
                else
                {
                    var addrFeature = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>()
                        .Features.Get<IServerAddressesFeature>();
                    var address = addrFeature?.Addresses.FirstOrDefault();
                    if (address is not null && Uri.TryCreate(address, UriKind.Absolute, out var uri))
                        BoundPort = uri.Port;
                    else
                        BoundPort = port;
                }
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
            }
        }

        throw new InvalidOperationException("Could not bind any port in the configured range.", lastException);
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (_app is null) return;
        await _app.StopAsync(ct);
        await _app.DisposeAsync();
        _app = null;
    }
}
