using System.IO.Pipelines;
using System.Net;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http.Features;

internal class HttpClientConnectionContext : ConnectionContext,
                IConnectionLifetimeFeature,
                IConnectionEndPointFeature,
                IConnectionItemsFeature,
                IConnectionIdFeature,
                IConnectionTransportFeature,
                IDuplexPipe
{
    private readonly TaskCompletionSource _executionTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private HttpClientConnectionContext()
    {
        Transport = this;

        Features.Set<IConnectionIdFeature>(this);
        Features.Set<IConnectionTransportFeature>(this);
        Features.Set<IConnectionItemsFeature>(this);
        Features.Set<IConnectionEndPointFeature>(this);
        Features.Set<IConnectionLifetimeFeature>(this);
    }

    public Task ExecutionTask => _executionTcs.Task;

    public override string ConnectionId { get; set; } = Guid.NewGuid().ToString();

    public override IFeatureCollection Features { get; } = new FeatureCollection();

    public override IDictionary<object, object?> Items { get; set; } = new ConnectionItems();
    public override IDuplexPipe Transport { get; set; }

    public override EndPoint? LocalEndPoint { get; set; }

    public override EndPoint? RemoteEndPoint { get; set; }

    public PipeReader Input { get; set; } = default!;

    public PipeWriter Output { get; set; } = default!;

    public override CancellationToken ConnectionClosed { get; set; }

    public HttpResponseMessage HttpResponseMessage { get; set; } = default!;

    public override void Abort()
    {
        HttpResponseMessage.Dispose();

        _executionTcs.TrySetCanceled();

        Input.CancelPendingRead();
        Output.CancelPendingFlush();
    }

    public override void Abort(ConnectionAbortedException abortReason)
    {
        Abort();
    }

    public override ValueTask DisposeAsync()
    {
        Abort();

        return base.DisposeAsync();
    }

    public static async ValueTask<ConnectionContext> ConnectAsync(HttpMessageInvoker invoker, Uri uri, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Version = new Version(2, 0)
        };
        var connection = new HttpClientConnectionContext();
        request.Content = new HttpClientConnectionContextContent(connection);
        var response = await invoker.SendAsync(request, cancellationToken).ConfigureAwait(false);
        connection.HttpResponseMessage = response;
        var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        connection.Input = PipeReader.Create(responseStream);

        return connection;
    }

    private class HttpClientConnectionContextContent : HttpContent
    {
        private readonly HttpClientConnectionContext _connectionContext;

        public HttpClientConnectionContextContent(HttpClientConnectionContext connectionContext)
        {
            _connectionContext = connectionContext;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            _connectionContext.Output = PipeWriter.Create(stream);

            // Immediately flush request stream to send headers
            // https://github.com/dotnet/corefx/issues/39586#issuecomment-516210081
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            await _connectionContext.ExecutionTask.ConfigureAwait(false);
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            _connectionContext.Output = PipeWriter.Create(stream);

            // Immediately flush request stream to send headers
            // https://github.com/dotnet/corefx/issues/39586#issuecomment-516210081
            await stream.FlushAsync().ConfigureAwait(false);

            await _connectionContext.ExecutionTask.ConfigureAwait(false);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }
    }
}