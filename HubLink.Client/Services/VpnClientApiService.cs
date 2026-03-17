namespace HubLink.Client.Services;

public class VpnClientApiService : IHostedService
{
    private readonly ILogger<VpnClientApiService> _logger;
    private readonly IVpnClient _vpnClientApi;
    private readonly VpnConfig _vpnConfig;
    private readonly IConfiguration _configuration;
    private readonly ConcurrentQueue<LogEntry> _logQueue;

    private WebApplication? _app;

    public VpnClientApiService(
        ILogger<VpnClientApiService> logger,
        IVpnClient vpnClientApi,
        VpnConfig vpnConfig,
        IConfiguration configuration)
    {
        _logger = logger;
        _vpnClientApi = vpnClientApi;
        _vpnConfig = vpnConfig;
        _configuration = configuration;
        _logQueue = new ConcurrentQueue<LogEntry>();
    }

    public async Task StartAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting VPN API Service");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Configure(_configuration.GetSection("Kestrel"));
        });

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
            });
        });

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.TypeInfoResolver = AppJsonContext.Default;
            options.SerializerOptions.WriteIndented = true;
        });

        var app = builder.Build();

        app.UseCors();

        var wwwrootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");

        if (!Directory.Exists(wwwrootPath))
        {
            _logger.LogWarning("wwwroot directory not found at {Path}, creating it", wwwrootPath);
            Directory.CreateDirectory(wwwrootPath);
        }
        else
        {
            _logger.LogInformation("Using wwwroot directory at {Path}", wwwrootPath);
        }

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(wwwrootPath),
            RequestPath = ""
        });

        app.MapGet("/", async context =>
        {
            var indexPath = Path.Combine(wwwrootPath, "index.html");
            if (File.Exists(indexPath))
            {
                await context.Response.SendFileAsync(indexPath);
            }
            else
            {
                _logger.LogError("index.html not found at {Path}", indexPath);
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync("index.html not found");
            }
        });

        app.MapGet("/api/status", HandleGetStatus);
        app.MapPost("/api/connect", HandleConnect);
        app.MapPost("/api/disconnect", HandleDisconnect);
        app.MapGet("/api/servers", HandleGetServers);
        app.MapPost("/api/servers", HandleAddServer);
        app.MapDelete("/api/servers/{name}", HandleDeleteServer);
        app.MapPut("/api/servers/{name}", HandleUpdateServer);
        app.MapPost("/api/servers/{name}/connect", HandleConnectToServer);
        app.MapGet("/api/traffic", HandleGetTraffic);
        app.MapGet("/api/traffic/stream", async context => await HandleTrafficStream(context));
        app.MapGet("/api/proxy/status", HandleGetProxyStatus);
        app.MapPost("/api/proxy/enable", HandleEnableProxy);
        app.MapPost("/api/proxy/disable", HandleDisableProxy);
        app.MapGet("/api/config", HandleGetConfig);
        app.MapGet("/api/logs", HandleGetLogs);
        app.MapGet("/api/status/stream", async context => await HandleStatusStream(context));

        _app = app;

        await _app.StartAsync(stoppingToken);

        _logger.LogInformation("VPN API Service started successfully");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("VPN API Service stopping...");

        if (_app != null)
        {
            await HandleDisconnect(cancellationToken);
            await _app.StopAsync(cancellationToken);
        }
    }

    private async Task<IResult> HandleGetStatus()
    {
        var (resultCode, status) = await _vpnClientApi.GetStatusAsync();
        
        if (resultCode != 0 || status == null)
        {
            return Results.BadRequest(ApiResponse<object>.Fail("Failed to get status"));
        }

        return Results.Ok(ApiResponse<VpnStatusResponse>.Ok(status));
    }

    private async Task<IResult> HandleConnect(CancellationToken stoppingToken)
    {
        try
        {
            var server = _vpnConfig.GetLastUsedServer();
            if (server == null)
            {
                _logger.LogWarning("No server configured");
                return Results.BadRequest(ApiResponse<object>.Fail("No server configured"));
            }

            _logger.LogInformation("Connecting to server: {ServerName}, URL: {ServerUrl}", server.Name, server.ServerUrl);
            var result = await _vpnClientApi.ConnectAsync(server.Name);
            
            if (result == 0)
            {
                return Results.Ok(ApiResponse<object>.Ok(null, "Connected successfully"));
            }
            else
            {
                return Results.BadRequest(ApiResponse<object>.Fail("Connection failed"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect");
            return Results.BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    private async Task<IResult> HandleDisconnect(CancellationToken stoppingToken)
    {
        try
        {
            var result = await _vpnClientApi.DisconnectAsync();
            
            if (result == 0)
            {
                return Results.Ok(ApiResponse<object>.Ok(null, "Disconnected successfully"));
            }
            else
            {
                return Results.BadRequest(ApiResponse<object>.Fail("Disconnect failed"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disconnect");
            return Results.BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    private async Task<IResult> HandleGetServers()
    {
        var (resultCode, servers) = await _vpnClientApi.GetServersAsync();
        
        if (resultCode != 0 || servers == null)
        {
            return Results.BadRequest(ApiResponse<object>.Fail("Failed to get servers"));
        }

        return Results.Ok(ApiResponse<ServerListResponse>.Ok(servers));
    }

    private async Task<IResult> HandleAddServer(HttpContext context)
    {
        try
        {
            var addRequest = await context.Request.ReadFromJsonAsync<AddServerRequest>();

            if (addRequest == null)
            {
                return Results.BadRequest(ApiResponse<object>.Fail("Invalid request"));
            }

            var server = new VpnServerConfig
            {
                Name = addRequest.Name,
                ServerUrl = addRequest.ServerUrl,
                LocalPort = addRequest.LocalPort,
                EncryptionKey = addRequest.EncryptionKey,
                EnableEncryption = addRequest.EnableEncryption,
                AutoReconnect = addRequest.AutoReconnect,
                ReconnectInterval = addRequest.ReconnectInterval,
                AutoProxy = addRequest.AutoProxy
            };

            _vpnConfig.AddServer(server);

            return Results.Ok(ApiResponse<object>.Ok(null, "Server added successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add server");
            return Results.BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    private async Task<IResult> HandleUpdateServer(string name, HttpContext context)
    {
        try
        {
            var server = _vpnConfig.GetServer(name);
            if (server == null)
            {
                return Results.NotFound(ApiResponse<object>.Fail("Server not found"));
            }

            var updateRequest = await context.Request.ReadFromJsonAsync<UpdateServerRequest>();

            if (updateRequest != null)
            {
                if (updateRequest.Name != null) server.Name = updateRequest.Name;
                if (updateRequest.ServerUrl != null) server.ServerUrl = updateRequest.ServerUrl;
                if (updateRequest.LocalPort.HasValue) server.LocalPort = updateRequest.LocalPort.Value;
                if (updateRequest.EncryptionKey != null) server.EncryptionKey = updateRequest.EncryptionKey;
                if (updateRequest.EnableEncryption.HasValue) server.EnableEncryption = updateRequest.EnableEncryption.Value;
                if (updateRequest.AutoReconnect.HasValue) server.AutoReconnect = updateRequest.AutoReconnect.Value;
                if (updateRequest.ReconnectInterval.HasValue) server.ReconnectInterval = updateRequest.ReconnectInterval.Value;
                if (updateRequest.AutoProxy.HasValue) server.AutoProxy = updateRequest.AutoProxy.Value;
            }

            return Results.Ok(ApiResponse<object>.Ok(null, "Server updated successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update server");
            return Results.BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    private IResult HandleDeleteServer(string name)
    {
        try
        {
            _vpnConfig.RemoveServer(name);

            return Results.Ok(ApiResponse<object>.Ok(null, "Server deleted successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete server");
            return Results.BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    private async Task<IResult> HandleConnectToServer(string name)
    {
        try
        {
            var server = _vpnConfig.GetServer(name);
            if (server == null)
            {
                return Results.NotFound(ApiResponse<object>.Fail("Server not found"));
            }

            _vpnConfig.LastUsedServer = name;
            // _vpnConfig.Save(_configPath);

            var result = await _vpnClientApi.ConnectAsync(name);

            if (result == 0)
            {
                return Results.Ok(ApiResponse<object>.Ok(null, "Connected successfully"));
            }
            else
            {
                return Results.BadRequest(ApiResponse<object>.Fail("Connection failed"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to server");
            return Results.BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    private async Task<IResult> HandleGetProxyStatus()
    {
        var (resultCode, status) = await _vpnClientApi.GetStatusAsync();
        
        if (resultCode != 0 || status == null)
        {
            return Results.BadRequest(ApiResponse<object>.Fail("Failed to get proxy status"));
        }

        var response = new ProxyStatusResponse
        {
            IsEnabled = status.IsConnected,
            HttpEnabled = status.IsConnected,
            HttpsEnabled = status.IsConnected
        };

        return Results.Ok(ApiResponse<ProxyStatusResponse>.Ok(response));
    }

    private async Task<IResult> HandleEnableProxy()
    {
        try
        {
            var result = await _vpnClientApi.EnableProxyAsync();
            
            if (result == 0)
            {
                return Results.Ok(ApiResponse<object>.Ok(null, "Proxy enabled successfully"));
            }
            else
            {
                return Results.BadRequest(ApiResponse<object>.Fail("Failed to enable proxy"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable proxy");
            return Results.BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    private async Task<IResult> HandleDisableProxy()
    {
        try
        {
            var result = await _vpnClientApi.DisableProxyAsync();
            
            if (result == 0)
            {
                return Results.Ok(ApiResponse<object>.Ok(null, "Proxy disabled successfully"));
            }
            else
            {
                return Results.BadRequest(ApiResponse<object>.Fail("Failed to disable proxy"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disable proxy");
            return Results.BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    private async Task<IResult> HandleGetTraffic()
    {
        var (resultCode, traffic) = await _vpnClientApi.GetTrafficAsync();
        
        if (resultCode != 0 || traffic == null)
        {
            return Results.BadRequest(ApiResponse<object>.Fail("Failed to get traffic"));
        }

        return Results.Ok(ApiResponse<TrafficInfo>.Ok(traffic));
    }

    private async Task HandleTrafficStream(HttpContext context)
    {
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.Append("Cache-Control", "no-cache");
        context.Response.Headers.Append("Connection", "keep-alive");
        context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
        context.Response.Headers.Append("X-Accel-Buffering", "no");
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");

        _logger.LogInformation("Traffic stream started");

        try
        {
            while (!context.RequestAborted.IsCancellationRequested)
            {
                var (resultCode, traffic) = await _vpnClientApi.GetTrafficAsync();
                
                if (resultCode == 0 && traffic != null)
                {
                    var jsonData = JsonSerializer.Serialize(ApiResponse<TrafficInfo>.Ok(traffic), JsonOptions.Default);
                    var eventData = $"data: {jsonData}\n\n";

                    try
                    {
                        await context.Response.WriteAsync(eventData, context.RequestAborted);
                        await context.Response.Body.FlushAsync(context.RequestAborted);
                    }
                    catch (ObjectDisposedException)
                    {
                        _logger.LogWarning("Traffic stream: ObjectDisposedException, client disconnected");
                        break;
                    }
                    catch (IOException)
                    {
                        _logger.LogWarning("Traffic stream: IOException, client disconnected");
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("Traffic stream: OperationCanceledException, stream stopped");
                        break;
                    }
                }

                try
                {
                    await Task.Delay(1000, context.RequestAborted);
                }
                catch (TaskCanceledException)
                {
                    _logger.LogInformation("Traffic stream: TaskCanceledException, stream stopped");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Traffic stream: Unexpected error");
        }
        finally
        {
            _logger.LogInformation("Traffic stream ended");
        }
    }

    private IResult HandleGetConfig()
    {
        var response = new ConfigInfo
        {
            ApiPort = _configuration.GetValue<int>("Api:Port", 5080),
            LogLevel = "Information",
            AutoReconnect = _vpnConfig.Servers.Any(s => s.AutoReconnect),
            ReconnectInterval = _vpnConfig.Servers.FirstOrDefault()?.ReconnectInterval ?? 5
        };

        return Results.Ok(ApiResponse<ConfigInfo>.Ok(response));
    }

    private IResult HandleGetLogs()
    {
        var logs = _logQueue.ToList();

        var response = new LogsResponse
        {
            Logs = logs,
            TotalCount = logs.Count
        };

        return Results.Ok(ApiResponse<LogsResponse>.Ok(response));
    }

    private async Task HandleStatusStream(HttpContext context)
    {
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.Append("Cache-Control", "no-cache");
        context.Response.Headers.Append("Connection", "keep-alive");
        context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
        context.Response.Headers.Append("X-Accel-Buffering", "no");
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");

        _logger.LogInformation("Status stream started");

        try
        {
            while (!context.RequestAborted.IsCancellationRequested)
            {
                var (resultCode, status) = await _vpnClientApi.GetStatusAsync();
                
                if (resultCode == 0 && status != null)
                {
                    var jsonData = JsonSerializer.Serialize(ApiResponse<VpnStatusResponse>.Ok(status), JsonOptions.Default);
                    var eventData = $"data: {jsonData}\n\n";

                    try
                    {
                        await context.Response.WriteAsync(eventData, context.RequestAborted);
                        await context.Response.Body.FlushAsync(context.RequestAborted);
                    }
                    catch (ObjectDisposedException)
                    {
                        _logger.LogWarning("Status stream: ObjectDisposedException, client disconnected");
                        break;
                    }
                    catch (IOException)
                    {
                        _logger.LogWarning("Status stream: IOException, client disconnected");
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("Status stream: OperationCanceledException, stream stopped");
                        break;
                    }
                }

                try
                {
                    await Task.Delay(1000, context.RequestAborted);
                }
                catch (TaskCanceledException)
                {
                    _logger.LogInformation("Status stream: TaskCanceledException, stream stopped");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Status stream: Unexpected error");
        }
        finally
        {
            _logger.LogInformation("Status stream ended");
        }
    }
}
