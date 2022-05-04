
using System;
using Microsoft.AspNetCore.Connections;

public static class WebHostBuilderExtensions
{
    public static IWebHostBuilder UseWebSocketTunnel(this IWebHostBuilder hostBuilder, Action<WebSocketTunnelOptions>? configure = null)
    {
        return hostBuilder.ConfigureServices(services =>
        {
            services.AddSingleton<IConnectionListenerFactory, WebSocketTunnelConnectionListenerFactory>();

            if (configure is not null)
            {
                services.Configure(configure);
            }
        });
    }
}
