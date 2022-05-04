using System.Net.WebSockets;
using System.Threading.Tasks.Sources;

internal class WebSocketStream : Stream, IValueTaskSource<object?>
{
    private readonly WebSocket _ws;
    private ManualResetValueTaskSourceCore<object?> _tcs = new() { RunContinuationsAsynchronously = true };

    public WebSocketStream(WebSocket ws)
    {
        _ws = ws;
    }

    internal ValueTask<object?> StreamCompleteTask => new(this, _tcs.Version);

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override void Flush()
    {
        throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await _ws.SendAsync(buffer, WebSocketMessageType.Binary, endOfMessage: false, cancellationToken);
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var result = await _ws.ReceiveAsync(buffer, cancellationToken);

        if (result.MessageType == WebSocketMessageType.Close)
        {
            return 0;
        }

        return result.Count;
    }

    protected override void Dispose(bool disposing)
    {
        // This might seem evil but we're using dispose to know if the stream
        // has been given discarded by http client. We trigger the continuation and take back ownership
        // of it here.
        _tcs.SetResult(null);
    }

    public object? GetResult(short token)
    {
        return _tcs.GetResult(token);
    }

    public void Reset()
    {
        _tcs.Reset();
    }

    public ValueTaskSourceStatus GetStatus(short token)
    {
        return _tcs.GetStatus(token);
    }

    public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
    {
        _tcs.OnCompleted(continuation, state, token, flags);
    }
}