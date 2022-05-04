using Microsoft.AspNetCore.Connections;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseWebSocketTunnel(o => o.MaxConnectionCount = 10);
builder.WebHost.ConfigureKestrel(o =>
{
    // Add the endpoint
    o.Listen(new UriEndPoint(new("https://localhost:7244/connect")));
});

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();
