var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
       .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));


// This is the HTTP/2 endpoint to register this app as part of the cluster endpoint
var url = "https://localhost:7244/connect-h2?clusterId=alpha";

builder.WebHost.UseTunnelTransport(url, options =>
{
    options.MaxConnectionCount = 1;
    options.Transport = TransportType.HTTP2;
});

var app = builder.Build();

app.MapReverseProxy();

app.Run();
