using System.IO.Pipelines;
using System.Net;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;
/// <summary>
/// This exists solely to track the lifetime of the connection
/// </summary>
internal class TrackLifetimeConnectionContext : ConnectionContext
{
    private readonly ConnectionContext _connection;
    private readonly TaskCompletionSource _executionTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TrackLifetimeConnectionContext(ConnectionContext connection)
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