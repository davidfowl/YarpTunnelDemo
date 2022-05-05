public class TunnelOptions
{
    public int MaxConnectionCount { get; set; } = 10;

    public TransportType Transport { get; set; } = TransportType.HTTP2;
}

public enum TransportType
{
    WebSockets,
    HTTP2
}