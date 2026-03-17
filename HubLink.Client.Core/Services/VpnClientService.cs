namespace HubLink.Client.Services;

public class VpnClientService : IHostedService
{
    private readonly TunnelConnectionService _connectionService;
    private readonly ILogger<VpnClientService> _logger;
    private readonly SystemProxyService _systemProxy;
    private readonly ITrafficStats _trafficStats;
    private readonly ConnectionStatus _connectionStatus;
    private Socket? _listener;
    private readonly VpnClientTunnelManager _clientTunnelManager;
    private TunnelInfo _vpnConfig;
    private VpnServerConfig? _currentServer;
    private Task _proxyTask = Task.CompletedTask;
    private readonly VpnClientMode _mode;
    private CancellationTokenSource? _serviceCts;
    private int _reconnectAttempts = 0;
    private const int MaxReconnectAttempts = 3;
    public bool IsConnected => _connectionStatus.State == ConnectionState.Connected;
    public VpnServerConfig? CurrentServer => _currentServer;
    public ITrafficStats TrafficStats => _trafficStats;
    public ConnectionStatus ConnectionStatus => _connectionStatus;

    public VpnClientService(
        ILogger<VpnClientService> logger,
        SystemProxyService systemProxy,
        TunnelConnectionService connectionService,
        ITrafficStats trafficStats,
        VpnClientTunnelManager clientTunnelManager,
        VpnClientMode mode = VpnClientMode.Library)
    {
        _connectionService = connectionService;
        _logger = logger;
        _systemProxy = systemProxy;
        _trafficStats = trafficStats;
        _connectionStatus = new ConnectionStatus();
        _clientTunnelManager = clientTunnelManager;
        _vpnConfig = new();
        _mode = mode;

        _connectionStatus.StateChanged += (sender, args) =>
        {
            _logger.LogDebug("Connection state changed: {OldState} -> {NewState}", args.OldState, args.NewState);
        };

        _connectionService.OnConnected += async (sender, args) =>
        {
            _logger.LogInformation("SignalR connection established. Connection ID: {ConnectionId}", _connectionService.ConnectionId);
            _connectionStatus.SetState(ConnectionState.Connected);

            _reconnectAttempts = 0;

            _vpnConfig.ClientId = _connectionService.ConnectionId ?? string.Empty;
            _vpnConfig.EnableEncryption = _currentServer.EnableEncryption;
            _vpnConfig.EncryptionKey = VpnPacketHelper.GetEncryptionKey(_currentServer.EncryptionKey);
            _vpnConfig.Version = "1.0";

            try
            {
                await _connectionService.StartTunnelAsync(_vpnConfig);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to build tunnel");
            }
        };
        _connectionService.OnEvent += async (sender, args) =>
        {
            if (args == "VpnConnected")
            {
                if (_mode == VpnClientMode.CommandLine)
                {
                    await StartLocalProxyAsync(_currentServer.LocalPort);
                }
            }
        };
        _connectionService.OnClosed += async (sender, error) =>
        {
            if (_connectionStatus.State == ConnectionState.Disconnecting)
            {
                _logger.LogInformation("Connection was intentionally disconnected, not reconnecting");
            }
            else
            {
                _reconnectAttempts++;
                _logger.LogWarning("SignalR connection closed (attempt {Attempt}/{MaxAttempts}): {Error}", _reconnectAttempts, MaxReconnectAttempts, error?.Message ?? "Unknown error");

                if (_reconnectAttempts < MaxReconnectAttempts)
                {
                    _logger.LogInformation("Attempting to reconnect ({Attempt}/{MaxAttempts})...", _reconnectAttempts, MaxReconnectAttempts);
                    await _connectionService.ConnectAsync();
                    return;
                }
            }

            _connectionStatus.SetState(ConnectionState.Disconnected, error?.Message ?? "Unknown error");
            if (_mode == VpnClientMode.CommandLine)
            {
                await StopLocalProxyAsync();
            }
        };
    }

    public async Task<bool> EnableSystemProxyAsync()
    {
        _logger.LogInformation("Enabling system proxy...");
        var success = await _systemProxy.EnableProxyAsync("127.0.0.1", _currentServer.LocalPort, true);
        if (success)
        {
            _logger.LogInformation("System proxy enabled successfully");
        }
        else
        {
            _logger.LogWarning("Failed to enable system proxy");
        }
        return success;
    }

    public async Task<bool> DisableSystemProxyAsync()
    {
        _logger.LogInformation("Disabling system proxy...");
        var success = await _systemProxy.DisableProxyAsync();
        if (success)
        {
            _logger.LogInformation("System proxy disabled successfully");
        }
        else
        {
            _logger.LogWarning("Failed to disable system proxy");
        }
        return success;
    }

    public async Task<ProxyStatus> GetSystemProxyStatusAsync()
    {
        return await _systemProxy.GetProxyStatusAsync();
    }

    public async Task<bool> ConfigurePACFileAsync(string pacUrl)
    {
        _logger.LogInformation("Configuring PAC file: {PacUrl}", pacUrl);
        return await _systemProxy.ConfigurePACFileAsync(pacUrl);
    }

    public async Task<bool> RestoreSystemProxyAsync()
    {
        _logger.LogInformation("Restoring system proxy settings");
        return await _systemProxy.RestoreProxyAsync();
    }

    public async Task ConnectToProxyAsync(VpnServerConfig server, CancellationToken cancellationToken = default)
    {
        try
        {
            _currentServer = server;

            if (_vpnConfig == null)
            {
                _logger.LogInformation("No server configured, waiting for configuration...");
                while (_vpnConfig == null && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
            }

            _connectionStatus.SetState(ConnectionState.Connecting);
            _logger.LogInformation("Connecting to VPN server: {ServerName}", _currentServer.Name);
            _logger.LogInformation("Server URL: {ServerUrl}", _currentServer.ServerUrl);
            _logger.LogInformation("Local Port: {LocalPort}", _currentServer.LocalPort);
            _logger.LogInformation("Encryption: {Encryption}", _currentServer.EnableEncryption);

            await _connectionService.ConnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _connectionStatus.SetState(ConnectionState.Error, ex.Message);
            _logger.LogError(ex, "VPN service error");
        }
    }

    public async Task DisconnectFromProxyAsync(CancellationToken cancellationToken = default)
    {
        _connectionStatus.SetState(ConnectionState.Disconnecting);

        if (_connectionService.IsConnected)
        {
            await _connectionService.DisconnectAsync(cancellationToken);
        }

        _logger.LogInformation("VPN Client Service stopped");
    }

    public async Task StartLocalProxyAsync(int localPort)
    {
        if (_listener != null)
        {
            _logger.LogInformation("Local proxy already running on port {Port}", localPort);
            return;
        }

        _serviceCts = CancellationTokenSource.CreateLinkedTokenSource(_connectionService.ConnectionCancellationToken);

        _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _listener.Bind(new IPEndPoint(IPAddress.Loopback, localPort));
        _listener.Listen(100);

        _logger.LogInformation("Local proxy started on port {Port}", localPort);
        _logger.LogInformation("Ready to handle requests to any target address");

        _proxyTask = Task.Run(async () =>
        {
            try
            {
                while (_listener != null && !_serviceCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var clientSocket = await _listener.AcceptAsync(_serviceCts.Token);
                        _ = HandleClientConnection(clientSocket);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Accept error");
                    }
                }
            }
            finally
            {
                _listener?.Close();
                _listener?.Dispose();
                _listener = null;
                _logger.LogInformation("Local proxy stopped");
            }
        }, _serviceCts.Token);

        _logger.LogInformation("AutoProxy setting: {AutoProxy}", _currentServer?.AutoProxy ?? false);

        if (_currentServer != null && _currentServer.AutoProxy)
        {
            _logger.LogInformation("Enabling system proxy...");
            await EnableSystemProxyAsync();
        }

        _logger.LogInformation("VPN connected successfully!");
    }

    public async Task StopLocalProxyAsync()
    {
        _serviceCts?.Cancel();
        _serviceCts?.Dispose();
        _serviceCts = null;

        if (_currentServer != null && _currentServer.AutoProxy)
        {
            await DisableSystemProxyAsync();
        }

        _listener?.Close();
        _listener?.Dispose();
        _listener = null;

        _clientTunnelManager.CloseAll();

        if (_proxyTask != null && !_proxyTask.IsCompleted)
        {
            try
            {
                await Task.WhenAny(_proxyTask, Task.Delay(5000));
            }
            catch { }
        }

        _logger.LogInformation("Local proxy stopped");
    }

    private async Task HandleClientConnection(Socket clientSocket)
    {
        var clientKey = Guid.NewGuid().ToString();

        if (_serviceCts == null || _serviceCts.Token.IsCancellationRequested)
        {
            _logger.LogWarning("VPN service not connected, rejecting client connection");
            clientSocket.Close();
            clientSocket.Dispose();
            return;
        }

        clientSocket.NoDelay = true;
        var clientTunnel = new VpnClientTunnel(clientKey, CancellationTokenSource.CreateLinkedTokenSource(_serviceCts.Token), _vpnConfig, _logger, _trafficStats);
        clientTunnel.OnClosed += async () =>
        {
            if (_clientTunnelManager.TryRemove(clientKey, out var removedClientTunnel))
            {
                _logger.LogInformation("{ClientKey} client disconnected from proxy, total clients: {ClientCount}", clientKey, _clientTunnelManager.Count);
            }

            await _connectionService.CloseClientTunnelStreamAsync(clientKey);
            _logger.LogDebug("Client tunnel {ClientKey} closed", clientKey);
        };
        await clientTunnel.AcceptAsync(clientSocket, () => _connectionService.OpenClientTunnelStreamAsync(clientKey, clientTunnel));

        _clientTunnelManager.Add(clientKey, clientTunnel);
        _logger.LogInformation("{ClientKey} client connected to proxy, total clients: {ClientCount}", clientKey, _clientTunnelManager.Count);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _connectionStatus.SetState(ConnectionState.Disconnecting);
        return Task.CompletedTask;
    }
}