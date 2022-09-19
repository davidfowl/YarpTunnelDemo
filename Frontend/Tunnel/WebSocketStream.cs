using System.Diagnostics;
using System.Net.WebSockets;
using System.Threading.Tasks.Sources;

internal class WebSocketStream : Stream, IValueTaskSource<object?>, ICloseable
{
    private readonly WebSocket _ws;
    private ManualResetValueTaskSourceCore<object?> _tcs = new() { RunContinuationsAsynchronously = true };
    private readonly object _sync = new();

    public WebSocketStream(WebSocket ws)
    {
        _ws = ws;
    }

    internal ValueTask<object?> StreamCompleteTask => new(this, _tcs.Version);
    public bool IsClosed => _ws.State != WebSocketState.Open;

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
    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return _ws.SendAsync(buffer, WebSocketMessageType.Binary, endOfMessage: false, cancellationToken);
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

    public void Abort()
    {
        // Debug.Assert(!Thread.CurrentThread.IsThreadPoolThread);
        _ws.Abort();

        // The shutdown path is currently synchronous but at least we're not blocking a threadpool thread
        // Attempt a graceful close
        //using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        //_ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", timeout.Token).GetAwaiter().GetResult();
        // Wait for closed to be sent back otherwise abort
        //if (_ws.State != WebSocketState.Closed)
        //{
        //    _ws.Abort();
        //}

        lock (_sync)
        {
            if (GetStatus(_tcs.Version) != ValueTaskSourceStatus.Pending)
            {
                return;
            }

            _tcs.SetResult(null);
        }
    }

    protected override void Dispose(bool disposing)
    {
        lock (_sync)
        {
            if (GetStatus(_tcs.Version) != ValueTaskSourceStatus.Pending)
            {
                return;
            }

            // This might seem evil but we're using dispose to know if the stream
            // has been given discarded by http client. We trigger the continuation and take back ownership
            // of it here.
            _tcs.SetResult(null);
        }
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