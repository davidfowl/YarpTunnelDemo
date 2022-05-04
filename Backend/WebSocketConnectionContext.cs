using System.IO.Pipelines;
using System.Net;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;

internal class WebSocketConnectionContext : ConnectionContext
{
    private readonly ConnectionContext _connection;
    private readonly TaskCompletionSource _executionTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource _cts = new();
    private readonly WebSocket _underlyingWebSocket;

    public WebSocketConnectionContext(WebSocket underlyingWebSocket, ConnectionContext connection)
    {
        _underlyingWebSocket = underlyingWebSocket;
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
        get => _cts.Token;
        set { }
    }

    public override void Abort()
    {
        _connection.Abort();
        _cts.Cancel();
        _underlyingWebSocket.Abort();
    }

    public override void Abort(ConnectionAbortedException abortReason)
    {
        _connection.Abort(abortReason);
        _cts.Cancel();
        _underlyingWebSocket.Abort();
    }

    public override ValueTask DisposeAsync()
    {
        _executionTcs.TrySetResult();
        return _connection.DisposeAsync();
    }
}