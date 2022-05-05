using System.Net.WebSockets;
using Yarp.ReverseProxy.Forwarder;


var tunnelFactory = new TunnelClientFactory();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
       .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddSingleton<IForwarderHttpClientFactory>(tunnelFactory);

builder.WebHost.ConfigureKestrel(o =>
{
    // https 7207
    o.ListenLocalhost(7244, c => c.UseHttps());

    // http
    o.ListenLocalhost(5244);

    //// TCP for tunnel
    // Need to send the cluster id over tcp, it could be a simple protocol
    // maybe JSON or something else.
    //o.Listen(IPAddress.Loopback, 5005, c => c.Run(async context =>
    //{
    //    var stream = new ConnectionContextStream(context);

    //    // This doesn't have a great way to map to individual clusters, but for demo
    //    // purposes, show how TCP can be used to connections from the backend
    //    while (!context.ConnectionClosed.IsCancellationRequested)
    //    {
    //        // Make this connection available for requests
    //        channel.Writer.TryWrite(stream);

    //        await stream.StreamCompleteTask;

    //        stream.Reset();
    //    }
    //}));
});

var app = builder.Build();

app.UseWebSockets();

app.MapReverseProxy();

app.MapPost("/connect-h2", async (HttpContext context, string clusterId, IHostApplicationLifetime lifetime) =>
{
    // HTTP/2 duplex stream
    if (context.Request.Protocol != HttpProtocol.Http2)
    {
        return Results.BadRequest();
    }

    var stream = new DuplexHttpStream(context);

    using var reg = lifetime.ApplicationStopping.Register(() => stream.Shutdown());

    var channel = tunnelFactory.GetConnectionChannel(clusterId);

    // Keep reusing this connection while, it's still open on the backend
    while (!context.RequestAborted.IsCancellationRequested)
    {
        // Make this connection available for requests
        channel.Writer.TryWrite(stream);

        await stream.StreamCompleteTask;

        stream.Reset();
    }

    return Results.Empty;
});

// This path should only be exposed on an internal port, the backend connects
// to this endpoint to register a connection with a specific cluster. To further secure this, authentication
// could be added (shared secret, JWT etc etc)
app.MapGet("/connect-ws", async (HttpContext context, string clusterId, IHostApplicationLifetime lifetime) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        return Results.BadRequest();
    }

    var ws = await context.WebSockets.AcceptWebSocketAsync();

    var stream = new WebSocketStream(ws);

    var channel = tunnelFactory.GetConnectionChannel(clusterId);

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
