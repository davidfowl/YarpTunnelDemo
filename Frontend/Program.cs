var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
       .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddTunnelServices();

builder.WebHost.ConfigureKestrel(o =>
{
    // https 7207
    o.ListenLocalhost(7244, c => c.UseHttps());

    // http
    o.ListenLocalhost(5244);
});

var app = builder.Build();

app.UseWebSockets();

app.MapReverseProxy();

app.MapTunnelEndpoints();

app.Run();
