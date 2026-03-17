namespace HubLink.Server.Services;

public class VpnTunnelService(IHubContext<VpnHub> hubContext, ILogger<VpnTunnelService> logger, VpnClientTunnelManager clientTunnelManager, TcpConnectionPool connectionPool, DnsResolver dnsResolver)
{
    private readonly ConcurrentDictionary<string, TunnelInfo> _tunnelInfos = new();

    public void StartTunnelAsync(string clientId, TunnelInfo tunnelInfo)
    {
        _tunnelInfos[clientId] = tunnelInfo;
        logger.LogInformation("Tunnel created for {ClientId}, EnableEncryption={EnableEncryption}, EncryptionKeyLength={KeyLength}", clientId, tunnelInfo.EnableEncryption, tunnelInfo.EncryptionKey.Length);
    }

    public bool TunnelExists(string clientId)
    {
        return _tunnelInfos.ContainsKey(clientId);
    }

    public void StopTunnelAsync(string clientId)
    {
        var tunnelsToClose = clientTunnelManager.GetAll()
            .Where(kvp => kvp.Value.TunnelId == clientId)
            .Select(kvp => kvp.Key)
            .ToList();
        foreach (var clientKey in tunnelsToClose)
        {
            CloseClientTunnelStreamAsync(clientKey);
        }
    }

    public async Task<ChannelReader<ReadOnlyMemory<byte>>> OpenClientTunnelStreamAsync(string clientId, string clientKey, string host, int port, ChannelReader<ReadOnlyMemory<byte>> remoteReader)
    {
        if (!_tunnelInfos.TryGetValue(clientId, out var tunnelInfo)) {
            throw new InvalidOperationException($"Tunnel not found for {clientId}");
        }
        
        ChannelReader<ReadOnlyMemory<byte>> tunnelReader;

        if (!clientTunnelManager.ContainsKey(clientKey))
        {
            if (string.IsNullOrEmpty(host) || port <= 0)
            {
                throw new InvalidOperationException($"Invalid tunnel configuration for {clientKey}");
            }

            var ipAddress = await dnsResolver.ResolveAddressAsync(host) ?? throw new InvalidOperationException($"Cannot resolve address: {host}");
            var endPoint = new IPEndPoint(ipAddress, port);

            var clientTunnel = new VpnClientTunnel(clientKey, new(), tunnelInfo, logger)
            {
                Host = host,
                Port = port,
                TunnelId = clientId
            };
            clientTunnelManager.Add(clientKey, clientTunnel);
            logger.LogInformation("{ClientKey} client connected to proxy, total clients: {ClientCount}", clientKey, clientTunnelManager.Count);

            clientTunnel.OnClosed += () =>
            {
                if (clientTunnelManager.TryRemove(clientKey, out var removedClientTunnel))
                {
                    logger.LogInformation("{ClientKey} client disconnected from proxy, total clients: {ClientCount}", clientKey, clientTunnelManager.Count);
                }

                logger.LogDebug("Client tunnel {ClientKey} closed", clientKey);
                return Task.CompletedTask;
            };

            clientTunnel.SetTunnelReader(null);
            tunnelReader = clientTunnel.Reader;

            clientTunnel.SetTunnelReader(remoteReader);
            await clientTunnel.ConnectAsync(endPoint);
        }
        else
        {
            var clientTunnel = clientTunnelManager[clientKey];

            clientTunnel.SetTunnelReader(null);
            tunnelReader = clientTunnel.Reader;

            clientTunnel.SetTunnelReader(remoteReader);
        }

        return tunnelReader;
    }

    public void CloseClientTunnelStreamAsync(string clientKey)
    {
        if (clientTunnelManager.TryRemove(clientKey, out var client))
        {
            client?.Close();
        }
    }
}
