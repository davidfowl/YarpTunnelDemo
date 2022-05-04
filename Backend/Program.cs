using System.Net;
using Microsoft.AspNetCore.Connections;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseTunnelTransport(o =>
{
    o.MaxConnectionCount = 1;
    o.Transport = TransportType.HTTP2;
});

builder.WebHost.ConfigureKestrel(o =>
{
    // TCP
    // o.Listen(IPAddress.Loopback, 5005);

    // WebSockets
    // o.Listen(new UriEndPoint(new("https://localhost:7244/connect-ws")));

    // H2
    o.Listen(new UriEndPoint(new("https://localhost:7244/connect-h2")));
});

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();
