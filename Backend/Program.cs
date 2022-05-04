using System.Net;
using Microsoft.AspNetCore.Connections;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseTunnelTransport(o =>
{
    o.MaxConnectionCount = 1;
    o.Transport = TransportType.TCP;
});

builder.WebHost.ConfigureKestrel(o =>
{
    // Add the endpoint
    o.Listen(IPAddress.Loopback, 5005);

    //  new UriEndPoint(new("https://localhost:7244/connect")));
});

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();
