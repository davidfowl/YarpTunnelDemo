using System.Net;

internal class UriEndPoint2 : IPEndPoint
{
    public Uri? Uri { get; }

    public UriEndPoint2(Uri uri) :
        this(0, 0)
    {
        Uri = uri;
    }

    public UriEndPoint2(long address, int port) : base(address, port)
    {
    }
}
