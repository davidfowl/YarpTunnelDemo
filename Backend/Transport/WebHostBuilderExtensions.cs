
using System;
using Microsoft.AspNetCore.Connections;

public static class WebHostBuilderExtensions
{
    public static IWebHostBuilder UseTunnelTransport(this IWebHostBuilder hostBuilder, string url, Action<TunnelOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(url);

        hostBuilder.ConfigureKestrel(options =>
        {
            options.Listen(new UriEndPoint(new Uri(url)));
        });

        return hostBuilder.ConfigureServices(services =>
        {
            services.AddSingleton<IConnectionListenerFactory, TunnelConnectionListenerFactory>();

            if (configure is not null)
            {
                services.Configure(configure);
            }
        });
    }
}
