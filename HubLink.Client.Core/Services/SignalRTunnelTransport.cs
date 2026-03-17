using Microsoft.AspNetCore.SignalR.Client;

namespace HubLink.Client.Services;

public class SignalRTunnelTransport : ITunnelTransport
{
    private readonly ILogger<SignalRTunnelTransport>? _logger;
    private readonly string _serverUrl;
    private readonly string _apiKey;

    private HubConnection? _connection;
    private CancellationTokenSource? _connectionCts;
    private TunnelInfo? _tunnelConfig;

    public TransportType TransportType => TransportType.SignalR;
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;
    public string? ConnectionId { get; private set; }
    public CancellationToken ConnectionCancellationToken => _connectionCts?.Token ?? CancellationToken.None;

    public event EventHandler<string>? OnConnected;
    public event EventHandler<Exception>? OnClosed;
    public event EventHandler<string>? OnEvent;

    public SignalRTunnelTransport(
        string serverUrl,
        string? apiKey = null,
        ILogger<SignalRTunnelTransport>? logger = null)
    {
        _serverUrl = serverUrl;
        _apiKey = apiKey ?? "your-secret-api-key-change-this-in-production";
        _logger = logger;
    }

    private void SetupConnectionHandlers(HubConnection connection)
    {
        connection.On<string>("VpnHub", (eventName) =>
        {
            _logger?.LogDebug("Received event: {EventName}", eventName);
            OnEvent?.Invoke(this, eventName);
        });
        connection.On<string>("Connected", async (clientId) =>
        {
            _logger?.LogInformation("SignalR connected successfully. Client ID: {ClientId}", clientId);
            await OnConnectedAsync(clientId);

            OnConnected?.Invoke(this, clientId);
        });
        connection.Closed += (error) =>
        {
            if (error != null)
            {
                _logger?.LogError(error, "SignalR connection closed: {ErrorType} - {ErrorMessage}", error.GetType().Name, error.Message);
            }
            else
            {
                _logger?.LogError("SignalR connection closed: Unknown error");
            }
            _connectionCts?.Cancel();
            _connectionCts?.Dispose();
            _connectionCts = null;
            OnClosed?.Invoke(this, error ?? new Exception("Unknown error"));
            return Task.CompletedTask;
        };
    }

    protected virtual async Task OnConnectedAsync(string connectionId)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("SignalR connection is not established");
        }

        ConnectionId = connectionId;

        _connectionCts?.Cancel();
        _connectionCts?.Dispose();
        _connectionCts = null;

        var cts = new CancellationTokenSource();
        _connectionCts = cts;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var httpHandler = new SocketsHttpHandler
        {
            UseProxy = false,
            AutomaticDecompression = DecompressionMethods.All,
            MaxConnectionsPerServer = 100
        };

        _connection = new HubConnectionBuilder()
            .WithUrl(_serverUrl + "/vpnhub", options =>
            {
                options.Transports = HttpTransportType.WebSockets;
                options.HttpMessageHandlerFactory = _ => httpHandler;
                options.WebSocketConfiguration = sockets =>
                {
                    sockets.SetRequestHeader("X-Client-Type", "BinaryStream");
                    sockets.SetRequestHeader("X-API-Key", _apiKey);
                    sockets.SetRequestHeader("X-Client-Version", "1.0");
                    sockets.KeepAliveInterval = TimeSpan.FromSeconds(30);
                };
                options.Headers["X-API-Key"] = _apiKey;
                options.CloseTimeout = TimeSpan.FromSeconds(30);
            })
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Warning);
            })
            .WithServerTimeout(TimeSpan.FromSeconds(60))
            .WithKeepAliveInterval(TimeSpan.FromSeconds(30))
            .Build();

        SetupConnectionHandlers(_connection);

        _logger?.LogInformation("Attempting to connect to SignalR server...");
        await _connection.StartAsync(cancellationToken);
        _logger?.LogInformation("SignalR connection established");
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
        {
            await StopTunnelAsync();
        }

        _logger?.LogInformation("Disconnecting from SignalR server...");
        await (_connection?.StopAsync(cancellationToken) ?? Task.CompletedTask);
        _connection = null;
        _logger?.LogInformation("SignalR connection stopped");
    }

    public async Task StartTunnelAsync(TunnelInfo config)
    {
        if (_connection == null)
        {
            return;
        }

        _logger?.LogInformation("Opening tunnel...");

        _tunnelConfig = config;
        await InvokeAsync("StartTunnel", config);

        _logger?.LogInformation("Tunnel opened successfully");
    }

    public async Task StopTunnelAsync()
    {
        await InvokeAsync("StopTunnel");
    }

    public async Task<ChannelReader<ReadOnlyMemory<byte>>> OpenClientTunnelStreamAsync(string clientKey, VpnClientTunnel client)
    {
        return await StreamAsChannelAsync("OpenClientTunnelStream", clientKey, client.Host, client.Port, client.Reader);
    }

    public async Task CloseClientTunnelStreamAsync(string clientKey)
    {
        await InvokeAsync("CloseClientTunnelStream", clientKey);
    }

    public async Task InvokeAsync(string methodName)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("SignalR connection is not established");
        }
        await (_connection?.InvokeAsync(methodName) ?? Task.CompletedTask);
    }

    public async Task InvokeAsync(string methodName, object args)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("SignalR connection is not established");
        }
        await (_connection?.InvokeAsync(methodName, args) ?? Task.CompletedTask);
    }

    public async Task InvokeAsync(string methodName, object args1, object args2, object args3)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("SignalR connection is not established");
        }
        await (_connection?.InvokeAsync(methodName, args1, args2, args3, ConnectionCancellationToken) ?? Task.CompletedTask);
    }

    public async Task<T> InvokeAsync<T>(string methodName, object args)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("SignalR connection is not established");
        }

        return await (_connection?.InvokeAsync<T>(methodName, args, ConnectionCancellationToken) ?? Task.FromResult(default(T))!);
    }

    public async Task<ChannelReader<T>> StreamAsChannelAsync<T>(string methodName, ChannelReader<T> channel, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("SignalR connection is not established");
        }

        return await _connection!.StreamAsChannelAsync<T>(methodName, channel, ConnectionCancellationToken);
    }

    public async Task<ChannelReader<T>> StreamAsChannelAsync<T>(string methodName, object arg1, object arg2, object arg3, ChannelReader<T> channel, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("SignalR connection is not established");
        }

        return await _connection!.StreamAsChannelAsync<T>(methodName, arg1, arg2, arg3, channel, ConnectionCancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await (_connection?.DisposeAsync() ?? ValueTask.CompletedTask);
        _connectionCts?.Cancel();
        _connectionCts?.Dispose();
        _connectionCts = null;
    }
}

public class SignalRTunnelTransportFactory : ITunnelTransportFactory
{
    public ITunnelTransport CreateTransport(string serverUrl, string? apiKey = null, ILogger? logger = null)
    {
        return new SignalRTunnelTransport(serverUrl, apiKey, logger as ILogger<SignalRTunnelTransport>);
    }
}
