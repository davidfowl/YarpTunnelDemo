var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
       .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddTunnelServices();

var app = builder.Build();

app.MapReverseProxy();

// Uncomment to support websocket connections
app.MapWebSocketTunnel("/connect-ws");

// Auth can be added to this endpoint and we can restrict it to certain points
// to avoid exteranl traffic hitting it
app.MapHttp2Tunnel("/connect-h2");

app.Run();
