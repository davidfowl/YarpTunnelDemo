using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// This has the core logic that creates and maintains connections to the proxu.
/// </summary>
internal class TunnelConnectionListener : IConnectionListener
{
    private readonly SemaphoreSlim _connectionLock;
    private readonly ConcurrentDictionary<ConnectionContext, ConnectionContext> _connections = new();
    private readonly TunnelOptions _options;
    private readonly CancellationTokenSource _closedCts = new();
    private readonly SocketConnectionContextFactory _contextFactory = new(new SocketConnectionFactoryOptions(), NullLogger.Instance);
    private readonly HttpMessageInvoker _httpMessageInvoker = new(new SocketsHttpHandler
    {
        EnableMultipleHttp2Connections = true
    });

    public TunnelConnectionListener(TunnelOptions options, EndPoint endpoint)
    {
        _options = options;
        _connectionLock = new(options.MaxConnectionCount);
        EndPoint = endpoint;

        switch (options.Transport)
        {
            case TransportType.HTTP2:
            case TransportType.WebSockets:
                if (endpoint is not UriEndPoint)
                {
                    throw new NotSupportedException($"UriEndPoint is required for {options.Transport} transport");
                }
                break;
            case TransportType.TCP:
                if (endpoint is not IPEndPoint)
                {
                    throw new NotSupportedException("IPEndPoint is required for tcp transport");
                }
                break;
            default:
                break;
        }
    }

    public EndPoint EndPoint { get; }

    public async ValueTask<ConnectionContext?> AcceptAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken = CancellationTokenSource.CreateLinkedTokenSource(_closedCts.Token, cancellationToken).Token;

            // Kestrel will keep an active accept call open as long as the transport is active
            await _connectionLock.WaitAsync(cancellationToken);

            var connection = new TrackLifetimeConnectionContext(_options.Transport switch
            {
                TransportType.WebSockets => await WebSocketConnectionContext.ConnectAsync(((UriEndPoint)EndPoint).Uri, cancellationToken),
                TransportType.TCP => await ConnectSocketAsync((IPEndPoint)EndPoint),
                TransportType.HTTP2 => await HttpClientConnectionContext.ConnectAsync(_httpMessageInvoker, ((UriEndPoint)EndPoint).Uri, cancellationToken),
                _ => throw new NotSupportedException(),
            });

            // Track this connection lifetime
            _connections.TryAdd(connection, connection);

            _ = Task.Run(async () =>
            {
                // When the connection is disposed, release it
                await connection.ExecutionTask;

                _connections.TryRemove(connection, out _);
                // Allow more connections in
                _connectionLock.Release();
            },
            cancellationToken);

            return connection;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private async ValueTask<ConnectionContext> ConnectSocketAsync(IPEndPoint endPoint)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        await socket.ConnectAsync(endPoint);
        return _contextFactory.Create(socket);
    }

    public async ValueTask DisposeAsync()
    {
        List<Task>? tasks = null;

        foreach (var (_, connection) in _connections)
        {
            tasks ??= new();
            tasks.Add(connection.DisposeAsync().AsTask());
        }

        if (tasks is null)
        {
            return;
        }

        await Task.WhenAll(tasks);
    }

    public ValueTask UnbindAsync(CancellationToken cancellationToken = default)
    {
        _closedCts.Cancel();

        foreach (var (_, connection) in _connections)
        {
            // REVIEW: Graceful?
            connection.Abort();
        }

        return ValueTask.CompletedTask;
    }
}