using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.Http.Features;
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

    private class WebSocketTunnelConnectionListener : IConnectionListener
    {
        private readonly Uri _uri;
        private readonly SemaphoreSlim _connectionLock;
        private readonly ConcurrentQueue<ConnectionContext> _connections = new();
        private readonly WebSocketTunnelOptions _options;

        public WebSocketTunnelConnectionListener(WebSocketTunnelOptions options, Uri uri)
        {
            _uri = uri;
            _options = options;
            _connectionLock = new(options.MaxConnectionCount);
        }

        public EndPoint EndPoint { get; init; } = default!;

        public async ValueTask<ConnectionContext?> AcceptAsync(CancellationToken cancellationToken = default)
        {
            await _connectionLock.WaitAsync(cancellationToken);

            var options = new HttpConnectionOptions
            {
                Url = _uri,
                Transports = HttpTransportType.WebSockets,
                SkipNegotiation = true,
                WebSocketConfiguration = c =>
                {
                    c.KeepAliveInterval = TimeSpan.FromSeconds(5);
                }
            };

            var httpConnection = new HttpConnection(options, null);
            await httpConnection.StartAsync(TransferFormat.Binary, cancellationToken);

            var connection = new WebSocketConnectionContext(httpConnection);
            _connections.Enqueue(connection);

            _ = Task.Run(async () =>
            {
                await connection.ExecutionTask;
                _connectionLock.Release();
            },
            cancellationToken);

            return connection;
        }

        public async ValueTask DisposeAsync()
        {
            List<Task>? tasks = null;

            while (_connections.TryDequeue(out var connection))
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
            while (_connections.TryDequeue(out var connection))
            {
                // REVIEW: Graceful?
                connection.Abort();
            }

            return ValueTask.CompletedTask;
        }

        // This exists solely to track the lifetime of the connection
        private class WebSocketConnectionContext : ConnectionContext
        {
            private readonly ConnectionContext _connection;
            private readonly TaskCompletionSource _executionTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public WebSocketConnectionContext(ConnectionContext connection)
            {
                _connection = connection;
            }

            public Task ExecutionTask => _executionTcs.Task;

            public override string ConnectionId
            {
                get => _connection.ConnectionId;
                set => _connection.ConnectionId = value;
            }

            public override IFeatureCollection Features => _connection.Features;

            public override IDictionary<object, object?> Items
            {
                get => _connection.Items;
                set => _connection.Items = value;
            }

            public override IDuplexPipe Transport
            {
                get => _connection.Transport;
                set => _connection.Transport = value;
            }

            public override EndPoint? LocalEndPoint
            {
                get => _connection.LocalEndPoint;
                set => _connection.LocalEndPoint = value;
            }

            public override EndPoint? RemoteEndPoint
            {
                get => _connection.RemoteEndPoint;
                set => _connection.RemoteEndPoint = value;
            }

            public override CancellationToken ConnectionClosed
            {
                get => _connection.ConnectionClosed;
                set => _connection.ConnectionClosed = value;
            }

            public override void Abort()
            {
                _connection.Abort();
            }

            public override void Abort(ConnectionAbortedException abortReason)
            {
                _connection.Abort(abortReason);
            }

            public override ValueTask DisposeAsync()
            {
                _executionTcs.TrySetResult();
                return _connection.DisposeAsync();
            }
        }
    }
}