using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;

/// <summary>
/// This has the core logic that creates and maintains connections to the proxu.
/// </summary>
internal class WebSocketTunnelConnectionListener : IConnectionListener
{
    private readonly Uri _uri;
    private readonly SemaphoreSlim _connectionLock;
    private readonly ConcurrentDictionary<ConnectionContext, ConnectionContext> _connections = new();
    private readonly WebSocketTunnelOptions _options;
    private CancellationTokenSource _closedCts = new();

    public WebSocketTunnelConnectionListener(WebSocketTunnelOptions options, Uri uri)
    {
        _uri = uri;
        _options = options;
        _connectionLock = new(options.MaxConnectionCount);
    }

    public EndPoint EndPoint { get; init; } = default!;

    public async ValueTask<ConnectionContext?> AcceptAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken = CancellationTokenSource.CreateLinkedTokenSource(_closedCts.Token, cancellationToken).Token;

            // Kestrel will keep an active accept call open as long as the transport is active
            await _connectionLock.WaitAsync(cancellationToken);

            ClientWebSocket? underlyingWebSocket = null;
            var options = new HttpConnectionOptions
            {
                Url = _uri,
                Transports = HttpTransportType.WebSockets,
                SkipNegotiation = true,
                WebSocketConfiguration = c =>
                {
                    c.KeepAliveInterval = TimeSpan.FromSeconds(5);
                },
                WebSocketFactory = async (context, cancellationToken) =>
                {
                    underlyingWebSocket = new ClientWebSocket();
                    await underlyingWebSocket.ConnectAsync(context.Uri, cancellationToken);
                    return underlyingWebSocket;
                }
            };

            var httpConnection = new HttpConnection(options, null);
            await httpConnection.StartAsync(TransferFormat.Binary, cancellationToken);

            var connection = new WebSocketConnectionContext(underlyingWebSocket!, httpConnection);
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