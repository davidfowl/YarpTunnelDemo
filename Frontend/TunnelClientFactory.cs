using System.Collections.Concurrent;
using System.Threading.Channels;
using Yarp.ReverseProxy.Forwarder;

/// <summary>
/// The factory that YARP will use the create outbound connections by cluster id.
/// </summary>
internal class TunnelClientFactory : IForwarderHttpClientFactory
{
    private readonly ConcurrentDictionary<string, (HttpMessageInvoker, Channel<Stream>)> _clusterConnections = new();

    public Channel<Stream> GetConnectionChannel(string clusterId)
    {
        var (_, channel) = GetOrCreateEntry(clusterId);
        return channel;
    }

    public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context)
    {
        var (invoker, _) = GetOrCreateEntry(context.ClusterId);

        return invoker;
    }

    private (HttpMessageInvoker, Channel<Stream>) GetOrCreateEntry(string clusterId)
    {
        return _clusterConnections.GetOrAdd(clusterId, static (id) =>
        {
            // The connection pool we're going to use for this cluster
            var channel = Channel.CreateUnbounded<Stream>();

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

            return (new HttpMessageInvoker(handler), channel);
        });
    }
}
