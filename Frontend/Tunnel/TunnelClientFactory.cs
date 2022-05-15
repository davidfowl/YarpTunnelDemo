using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading.Channels;
using Yarp.ReverseProxy.Forwarder;

/// <summary>
/// The factory that YARP will use the create outbound connections by host name.
/// </summary>
internal class TunnelClientFactory : ForwarderHttpClientFactory
{
    private readonly ConcurrentDictionary<string, Channel<Stream>> _clusterConnections = new();

    public Channel<Stream> GetConnectionChannel(string host)
    {
        return _clusterConnections.GetOrAdd(host, _ => Channel.CreateUnbounded<Stream>());
    }

    public void RemoveHost(string host) => _clusterConnections.TryRemove(host, out _);

    protected override void ConfigureHandler(ForwarderHttpClientContext context, SocketsHttpHandler handler)
    {
        base.ConfigureHandler(context, handler);

        var previous = handler.ConnectCallback ?? DefaultConnectCallback;

        static async ValueTask<Stream> DefaultConnectCallback(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
        {
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(context.DnsEndPoint, cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        handler.ConnectCallback = (context, cancellationToken) =>
        {
            if (_clusterConnections.TryGetValue(context.DnsEndPoint.Host, out var channel))
            {
                return channel.Reader.ReadAsync(cancellationToken);
            }
            return previous(context, cancellationToken);
        };
    }
}
