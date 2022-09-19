using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading.Channels;
using Yarp.ReverseProxy.Forwarder;

/// <summary>
/// The factory that YARP will use the create outbound connections by host name.
/// </summary>
internal class TunnelClientFactory : ForwarderHttpClientFactory
{
    // TODO: These values should be populated by configuration so there's no need to remove
    // channels.
    private readonly ConcurrentDictionary<string, (Channel<int>, Channel<Stream>)> _clusterConnections = new();

    public (Channel<int>, Channel<Stream>) GetConnectionChannel(string host)
    {
        return _clusterConnections.GetOrAdd(host, _ => (Channel.CreateUnbounded<int>(), Channel.CreateUnbounded<Stream>()));
    }

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

        handler.ConnectCallback = async (context, cancellationToken) =>
        {
            if (_clusterConnections.TryGetValue(context.DnsEndPoint.Host, out var pair))
            {
                var (requests, responses) = pair;

                // Ask for a connection
                await requests.Writer.WriteAsync(0, cancellationToken);

                while (true)
                {
                    var stream = await responses.Reader.ReadAsync(cancellationToken);

                    if (stream is ICloseable c && c.IsClosed)
                    {
                        // Ask for another connection
                        await requests.Writer.WriteAsync(0, cancellationToken);

                        continue;
                    }

                    return stream;
                }
            }
            return await previous(context, cancellationToken);
        };
    }
}
