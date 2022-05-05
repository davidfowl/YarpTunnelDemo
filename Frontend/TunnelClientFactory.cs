using System.Collections.Concurrent;
using System.Threading.Channels;
using Yarp.ReverseProxy.Forwarder;

/// <summary>
/// The factory that YARP will use the create outbound connections by cluster id.
/// </summary>
internal class TunnelClientFactory : ForwarderHttpClientFactory
{
    private readonly ConcurrentDictionary<string, Channel<Stream>> _clusterConnections = new();

    public Channel<Stream> GetConnectionChannel(string clusterId)
    {
        return _clusterConnections.GetOrAdd(clusterId, _ => Channel.CreateUnbounded<Stream>());
    }

    protected override void ConfigureHandler(ForwarderHttpClientContext context, SocketsHttpHandler handler)
    {
        base.ConfigureHandler(context, handler);

        // Overwrite if needed
        if (context.NewMetadata != null && context.NewMetadata.TryGetValue("tunnel", out var tunnelValue) &&
            bool.TryParse(tunnelValue, out var tunnel) && tunnel)
        {
            var channel = GetConnectionChannel(context.ClusterId);

            handler.ConnectCallback = (context, cancellationToken) => channel.Reader.ReadAsync(cancellationToken);
        }
    }
}
