public class TunnelOptions
{
    public int MaxConnectionCount { get; set; } = 10;

    public TransportType Transport { get; set; }
}

public enum TransportType
{
    WebSockets,
    HTTP2
}