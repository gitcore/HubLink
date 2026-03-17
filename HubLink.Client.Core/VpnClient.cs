namespace HubLink.Client;

public sealed class VpnClient(
    VpnConfig config,
    VpnClientService vpnClientService) : IVpnClient {

    public async Task<int> ConnectAsync(string serverName)
    {
        try
        {
            var server = config.GetServer(serverName);
            if (server == null)
            {
                return -1;
            }

            config.LastUsedServer = serverName;

            await vpnClientService.ConnectToProxyAsync(server);

            return 0;
        }
        catch (Exception)
        {
            return -1;
        }
    }

    public async Task<int> DisconnectAsync()
    {
        try
        {
            await vpnClientService.DisconnectFromProxyAsync();

            return 0;
        }
        catch (Exception)
        {
            return -1;
        }
    }

    public Task<(int resultCode, VpnStatusResponse status)> GetStatusAsync()
    {
        try
        {
            var connectionStatus = vpnClientService.ConnectionStatus;
            var trafficStats = vpnClientService.TrafficStats;
            var currentServer = vpnClientService.CurrentServer;

            var response = new VpnStatusResponse
            {
                IsConnected = vpnClientService.IsConnected,
                State = connectionStatus.GetStateString(),
                ErrorMessage = connectionStatus.ErrorMessage,
                Server = currentServer != null ? new VpnServerInfo
                {
                    Name = currentServer.Name,
                    ServerUrl = currentServer.ServerUrl,
                    LocalPort = currentServer.LocalPort,
                    EnableEncryption = currentServer.EnableEncryption,
                    AutoProxy = currentServer.AutoProxy
                } : null,
                Traffic = new TrafficInfo
                {
                    BytesSent = trafficStats.TotalBytesSent,
                    BytesReceived = trafficStats.TotalBytesReceived,
                    ActiveConnections = trafficStats.ActiveConnections,
                    UploadSpeed = trafficStats.GetUploadSpeed(),
                    DownloadSpeed = trafficStats.GetDownloadSpeed(),
                    Uptime = trafficStats.GetUptime().ToString(@"hh\:mm\:ss")
                },
                ConnectedAt = connectionStatus.LastConnectedTime?.ToUniversalTime(),
                ConnectionDuration = connectionStatus.GetConnectionDuration().TotalSeconds,
                ReconnectAttempts = connectionStatus.ReconnectAttempts
            };

            return Task.FromResult<(int, VpnStatusResponse)>((0, response));
        }
        catch (Exception)
        {
            return Task.FromResult<(int, VpnStatusResponse)>((-1, new VpnStatusResponse()));
        }
    }

    public Task<(int resultCode, ServerListResponse servers)> GetServersAsync()
    {
        try
        {
            var response = new ServerListResponse
            {
                Servers = config.Servers.Select(s => new VpnServerInfo
                {
                    Name = s.Name,
                    ServerUrl = s.ServerUrl,
                    LocalPort = s.LocalPort,
                    EnableEncryption = s.EnableEncryption,
                    AutoProxy = s.AutoProxy
                }).ToList(),
                LastUsedServer = config.LastUsedServer
            };

            return Task.FromResult<(int, ServerListResponse)>((0, response));
        }
        catch (Exception)
        {
            return Task.FromResult<(int, ServerListResponse)>((-1, new ServerListResponse()));
        }
    }

    public Task<(int resultCode, TrafficInfo traffic)> GetTrafficAsync()
    {
        try
        {
            var trafficStats = vpnClientService.TrafficStats;
            trafficStats.UpdateSpeed();

            var response = new TrafficInfo
            {
                BytesSent = trafficStats.TotalBytesSent,
                BytesReceived = trafficStats.TotalBytesReceived,
                ActiveConnections = trafficStats.ActiveConnections,
                UploadSpeed = trafficStats.GetUploadSpeed(),
                DownloadSpeed = trafficStats.GetDownloadSpeed(),
                Uptime = trafficStats.GetUptime().ToString(@"hh\:mm\:ss")
            };

            return Task.FromResult<(int, TrafficInfo)>((0, response));
        }
        catch (Exception)
        {
            return Task.FromResult<(int, TrafficInfo)>((-1, new TrafficInfo()));
        }
    }

    public Task<int> EnableProxyAsync()
    {
        try
        {
            var success = vpnClientService.EnableSystemProxyAsync().GetAwaiter().GetResult();
            return Task.FromResult(success ? 0 : -1);
        }
        catch (Exception)
        {
            return Task.FromResult(-1);
        }
    }

    public Task<int> DisableProxyAsync()
    {
        try
        {
            var success = vpnClientService.DisableSystemProxyAsync().GetAwaiter().GetResult();
            return Task.FromResult(success ? 0 : -1);
        }
        catch (Exception)
        {
            return Task.FromResult(-1);
        }
    }
}

