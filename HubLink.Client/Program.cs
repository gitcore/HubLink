var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());

var vpnConfig = VpnConfig.Load($"{AppContext.BaseDirectory}/vpn-config.json");

builder.Logging.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.AddConsole();
    loggingBuilder.AddDebug();
});

builder.Configuration.SetBasePath(AppContext.BaseDirectory);
builder.Configuration.AddJsonFile("appsettings.json", optional: true);
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddSingleton(vpnConfig);
builder.Services.AddSingleton<ITrafficStats, TrafficStats>();
builder.Services.AddSingleton<VpnClientTunnelManager>();

builder.Services.AddSingleton<ITunnelTransportFactory>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var transportType = config["Vpn:TransportType"] ?? "SignalR";
    
    return transportType.ToLowerInvariant() switch
    {
        "websocket" => new WebSocketTunnelTransportFactory(),
        "signalr" => new SignalRTunnelTransportFactory(),
        _ => new SignalRTunnelTransportFactory()
    };
});

builder.Services.AddSingleton<TunnelConnectionService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<TunnelConnectionService>>();
    var config = sp.GetRequiredService<IConfiguration>();
    var factory = sp.GetRequiredService<ITunnelTransportFactory>();

    var server = vpnConfig.GetLastUsedServer();
    var serverUrl = server?.ServerUrl ?? "http://localhost:4080";
    var apiKey = config["Vpn:ApiKey"] ?? "your-secret-api-key-change-this-in-production";

    var transport = factory.CreateTransport(serverUrl, apiKey, logger as ILogger<SignalRTunnelTransport>);
    return new TunnelConnectionService(logger, transport);
});

builder.Services.AddSingleton<SystemProxyService>();
builder.Services.AddSingleton<VpnClientService>(sp =>
{
    var signalRService = sp.GetRequiredService<TunnelConnectionService>();
    var logger = sp.GetRequiredService<ILogger<VpnClientService>>();
    var systemProxy = sp.GetRequiredService<SystemProxyService>();
    var trafficStats = sp.GetRequiredService<ITrafficStats>();
    var clientTunnelManager = sp.GetRequiredService<VpnClientTunnelManager>();
    return new VpnClientService(logger, systemProxy, signalRService, trafficStats, clientTunnelManager, VpnClientMode.CommandLine);
});

builder.Services.AddSingleton<IVpnClient, VpnClient>();
builder.Services.AddHostedService<VpnClientApiService>();

var host = builder.Build();

await host.RunAsync();