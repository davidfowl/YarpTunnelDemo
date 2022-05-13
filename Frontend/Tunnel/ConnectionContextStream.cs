using System.Buffers;
using System.IO.Pipelines;
using System.Threading.Tasks.Sources;
using Microsoft.AspNetCore.Connections;

internal class ConnectionContextStream : Stream, IValueTaskSource<object?>
{
    private readonly ConnectionContext _connectionContext;
    private ManualResetValueTaskSourceCore<object?> _tcs = new() { RunContinuationsAsynchronously = true };
    private readonly object _sync = new();

    public ConnectionContextStream(ConnectionContext connectionContext)
    {
        _connectionContext = connectionContext;
    }

    internal ValueTask<object?> StreamCompleteTask => new(this, _tcs.Version);

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await _connectionContext.Transport.Output.WriteAsync(buffer, cancellationToken);
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ReadResult result = await _connectionContext.Transport.Input.ReadAsync(cancellationToken).ConfigureAwait(false);
        return HandleReadResult(result, buffer.Span);
    }

    private int HandleReadResult(ReadResult result, Span<byte> buffer)
    {
        if (result.IsCanceled)
        {
            throw new OperationCanceledException();
        }

        ReadOnlySequence<byte> sequence = result.Buffer;
        long bufferLength = sequence.Length;
        SequencePosition consumed = sequence.Start;

        try
        {
            if (bufferLength != 0)
            {
                int actual = (int)Math.Min(bufferLength, buffer.Length);

                ReadOnlySequence<byte> slice = actual == bufferLength ? sequence : sequence.Slice(0, actual);
                consumed = slice.End;
                slice.CopyTo(buffer);

                return actual;
            }

            if (result.IsCompleted)
            {
                return 0;
            }
        }
        finally
        {
            _connectionContext.Transport.Input.AdvanceTo(consumed);
        }

        return 0;
    }

    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
    {
        // Delegate to CopyToAsync on the PipeReader
        return _connectionContext.Transport.Input.CopyToAsync(destination, cancellationToken);
    }

    internal void Shutdown()
    {
        _connectionContext.Abort();

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

    public object? GetResult(short token) => _tcs.GetResult(token);

    public void Reset() => _tcs.Reset();

    public ValueTaskSourceStatus GetStatus(short token) => _tcs.GetStatus(token);

    public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        => _tcs.OnCompleted(continuation, state, token, flags);

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
}
