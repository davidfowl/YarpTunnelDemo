var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
       .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddTunnelServices();

var app = builder.Build();

app.UseWebSockets();

app.MapReverseProxy();

app.MapTunnelEndpoints();

app.Run();
