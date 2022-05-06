using System.Collections.Concurrent;
using System.Net.Sockets;
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

        var previous = handler.ConnectCallback ?? DefaultConnectCallback;

        static async ValueTask<Stream> DefaultConnectCallback(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
        {
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            await socket.ConnectAsync(context.DnsEndPoint);
            return new NetworkStream(socket, ownsSocket: true);
        }

        var channel = GetConnectionChannel(context.ClusterId);

        handler.ConnectCallback = (context, cancellationToken) =>
        {
            if (context.DnsEndPoint.Host == "transport.tunnel")
            {
                return channel.Reader.ReadAsync(cancellationToken);
            }
            return previous(context, cancellationToken);
        };
    }
}
