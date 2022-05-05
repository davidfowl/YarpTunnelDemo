using Microsoft.AspNetCore.Connections;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
       .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.WebHost.UseTunnelTransport(o =>
{
    o.MaxConnectionCount = 1;
    o.Transport = TransportType.HTTP2;
});

builder.WebHost.ConfigureKestrel(o =>
{
    // WebSockets
    // o.Listen(new UriEndPoint(new("https://localhost:7244/connect-ws")));

    // H2
    o.Listen(new UriEndPoint(new("https://localhost:7244/connect-h2?clusterId=alpha")));
});

var app = builder.Build();

app.MapReverseProxy();

app.Run();
