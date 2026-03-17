
namespace HubLink.Server;
public class HubLinkServer : IHostedService
{
    private readonly ILogger<HubLinkServer> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _apiKey;
    private WebApplication? _app;

    public HubLinkServer(
        ILogger<HubLinkServer> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _apiKey = configuration["Vpn:ApiKey"] ?? "your-secret-api-key-change-this-in-production";
    }

    public async Task StartAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting HubLink Server");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });

        var transportType = _configuration["Vpn:TransportType"] ?? "SignalR";
        _logger.LogInformation("Using transport type: {TransportType}", transportType);

        if (transportType.Equals("SignalR", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddSignalR(options =>
            {
                options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
                options.HandshakeTimeout = TimeSpan.FromSeconds(30);
                options.KeepAliveInterval = TimeSpan.FromSeconds(30);
                options.EnableDetailedErrors = true;
                options.StreamBufferCapacity = 1000;
                options.MaximumParallelInvocationsPerClient = 200;
                options.MaximumReceiveMessageSize = 1024 * 1024;
            });
        }

        builder.Services.AddSingleton<ITrafficStats, ServerTrafficStats>();
        builder.Services.AddSingleton<DnsResolver>();
        builder.Services.AddSingleton<TcpConnectionPool>();
        builder.Services.AddSingleton<VpnClientTunnelManager>();
        builder.Services.AddSingleton<VpnTunnelService>();

        if (transportType.Equals("WebSocket", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddSingleton<WebSocketConnectionHandler>();
        }

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll",
                builder => builder
                    .AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader());
        });

        var app = builder.Build();

        // Force initialization of VpnTunnelService
        var vpnTunnelService = app.Services.GetRequiredService<VpnTunnelService>();

        app.Use(async (context, next) =>
        {
            var path = context.Request.Path;
            if (path.StartsWithSegments("/vpnhub") || path.StartsWithSegments("/ws"))
            {
                if (!context.Request.Headers.TryGetValue("X-API-Key", out var providedKey))
                {
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsync("API Key is required");
                    return;
                }

                if (providedKey != _apiKey)
                {
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsync("Invalid API Key");
                    return;
                }
            }

            await next();
        });

        app.UseCors("AllowAll");

        if (transportType.Equals("SignalR", StringComparison.OrdinalIgnoreCase))
        {
            app.MapHub<VpnHub>("/vpnhub", options =>
            {
                options.Transports = HttpTransportType.WebSockets;
            });
        }
        else if (transportType.Equals("WebSocket", StringComparison.OrdinalIgnoreCase))
        {
            app.Map("/ws", async context =>
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    var webSocketHandler = context.RequestServices.GetRequiredService<WebSocketConnectionHandler>();
                    var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    await webSocketHandler.HandleConnectionAsync(webSocket, context);
                }
                else
                {
                    context.Response.StatusCode = 400;
                }
            });
        }

        app.MapGet("/", () =>
        {
            return Results.Ok(new { 
                message = "VPN Server is running",
                transportType = transportType
            });
        });

        app.MapGet("/test/ip", async () =>
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                var response = await client.GetAsync("https://api.ipify.org?format=json");
                var content = await response.Content.ReadAsStringAsync();

                return Results.Ok(new
                {
                    success = true,
                    serverIp = content,
                    message = "Server public IP retrieved"
                });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get server IP: {ex.Message}");
            }
        });

        app.MapGet("/status", (VpnTunnelService tunnelService) =>
        {
            return Results.Ok(new
            {
                message = "VPN Server is running",
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                uptime = Environment.TickCount64 / 1000,
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                transportType = transportType
            });
        });

        _app = app;

        await _app.StartAsync(stoppingToken);

        var port = _configuration.GetValue<int>("Server:Port", 4080);
        _logger.LogInformation("HubLink Server started successfully on port {Port} with {TransportType}", port, transportType);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("HubLink Server stopping...");

        if (_app != null)
        {
            await _app.StopAsync(cancellationToken);
        }
    }
}
