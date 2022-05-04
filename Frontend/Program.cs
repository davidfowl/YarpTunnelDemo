using System.Net;
using System.Net.WebSockets;
using System.Threading.Channels;
using Microsoft.AspNetCore.Connections;
using Yarp.ReverseProxy.Forwarder;

// The queue of available connections. In a real implementation, we'd key this by cluster
var channel = Channel.CreateUnbounded<Stream>();

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(o =>
{
    // https 7207
    o.ListenLocalhost(7244, c => c.UseHttps());

    // http
    o.ListenLocalhost(5244);

    // TCP for tunnel
    o.Listen(IPAddress.Loopback, 5005, c => c.Run(async context =>
    {
        var stream = new ConnectionContextStream(context);

        // This doesn't have a great way to map to individual clusters, but for demo
        // purposes, show how TCP can be used to connections from the backend
        while (!context.ConnectionClosed.IsCancellationRequested)
        {
            // Make this connection available for requests
            channel.Writer.TryWrite(stream);

            await stream.StreamCompleteTask;

            stream.Reset();
        }
    }));
});

builder.Services.AddHttpForwarder();

var app = builder.Build();

var handler = new SocketsHttpHandler
{
    // Uncomment to demonstrate connection pooling disabling reusing connections
    // from the backend
    // PooledConnectionLifetime = TimeSpan.FromSeconds(0),
    ConnectCallback = async (context, cancellationToken) =>
    {
        return await channel.Reader.ReadAsync(cancellationToken);
    }
};
var client = new HttpMessageInvoker(handler);

app.UseWebSockets();

app.Map("{*path}", async (IHttpForwarder forwarder, HttpContext context) =>
{
    // This is hardcoded to a single backend, but that's just for the demo
    await forwarder.SendAsync(context, "http://woah/", client);

    return Results.Empty;
});

// This path should only be exposed on an internal port, the backend connects
// to this endpoint to register a connection with a specific cluster. To further secure this, authentication
// could be added (shared secret, JWT etc etc)
app.MapGet("/connect", async (HttpContext context, IHostApplicationLifetime lifetime) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        return Results.BadRequest();
    }

    // REVIEW: Use the path to register this connection per cluster

    var ws = await context.WebSockets.AcceptWebSocketAsync();

    var stream = new WebSocketStream(ws);

    // We should make this more graceful
    using var reg = lifetime.ApplicationStopping.Register(() => stream.Shutdown());

    // Keep reusing this connection while, it's still open on the backend
    while (ws.State == WebSocketState.Open)
    {
        // Make this connection available for requests
        channel.Writer.TryWrite(stream);

        await stream.StreamCompleteTask;

        stream.Reset();
    }

    return Results.Empty;
});

app.Run();
