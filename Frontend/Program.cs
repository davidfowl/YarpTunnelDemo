using System.Net.WebSockets;
using System.Threading.Channels;
using Yarp.ReverseProxy.Forwarder;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpForwarder();

var app = builder.Build();

// The queue of available connections. In a real implementation, we'd key this by cluster
var channel = Channel.CreateUnbounded<WebSocketStream>();

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
    // This is hardcoded to a single backend by can be for many
    await forwarder.SendAsync(context, "http://woah/", client);

    return Results.Extensions.Empty();
});

// This path should only be exposed on an internal port, the backend connects
// to this endpoint to register a connection with a specific cluster. To further secure this, authentication
// could be added (shared secret, JWT etc etc)
app.MapGet("/connect", async (HttpContext context) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        return Results.BadRequest();
    }

    // REVIEW: Use the path to register this connection per cluster

    var ws = await context.WebSockets.AcceptWebSocketAsync();

    var stream = new WebSocketStream(ws);

    // Keep reusing this connection while, it's still open on the backend
    while (ws.State == WebSocketState.Open)
    {
        // Make this connection available for requests
        channel.Writer.TryWrite(stream);

        await stream.StreamCompleteTask;

        stream.Reset();
    }

    return Results.Extensions.Empty();
});

app.Run();
