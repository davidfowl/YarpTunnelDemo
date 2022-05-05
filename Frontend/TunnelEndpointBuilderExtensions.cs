using System.Net.WebSockets;
using Yarp.ReverseProxy.Forwarder;

public static class TunnelEndpointBuilderExtensions
{
    public static IServiceCollection AddTunnelServices(this IServiceCollection services)
    {
        var tunnelFactory = new TunnelClientFactory();
        services.AddSingleton(tunnelFactory);
        services.AddSingleton<IForwarderHttpClientFactory>(tunnelFactory);
        return services;
    }

    public static void MapTunnelEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/connect-h2", static async (HttpContext context, string clusterId, TunnelClientFactory tunnelFactory, IHostApplicationLifetime lifetime) =>
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
        routes.MapGet("/connect-ws", static async (HttpContext context, string clusterId, TunnelClientFactory tunnelFactory, IHostApplicationLifetime lifetime) =>
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
    }
}
