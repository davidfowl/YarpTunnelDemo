using System.Net;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Options;

public class WebSocketTunnelConnectionListenerFactory : IConnectionListenerFactory
{
    private readonly WebSocketTunnelOptions _options;
    public WebSocketTunnelConnectionListenerFactory(IOptions<WebSocketTunnelOptions> options)
    {
        _options = options.Value;
    }

    public ValueTask<IConnectionListener> BindAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
    {
        if (endpoint is UriEndPoint uri)
        {
            return new(new WebSocketTunnelConnectionListener(_options, uri.Uri) { EndPoint = endpoint });
        }

        throw new NotSupportedException();
    }
}