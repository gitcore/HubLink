namespace HubLink.Shared;

public class VpnClientTunnelManager
{
    private readonly ConcurrentDictionary<string, VpnClientTunnel> _clientTunnels = new();
    private readonly ILogger<VpnClientTunnelManager> _logger;
    private readonly ITrafficStats _trafficStats;
    private CancellationTokenSource? _idleCheckCts;

    public VpnClientTunnelManager(ILogger<VpnClientTunnelManager> logger, ITrafficStats trafficStats)
    {
        _logger = logger;
        _trafficStats = trafficStats;

        StartIdleCheckTask();
    }

    public int Count => _clientTunnels.Count;

    public void Add(string clientKey, VpnClientTunnel clientTunnel)
    {
        _clientTunnels[clientKey] = clientTunnel;
        _trafficStats?.SetActiveConnections(_clientTunnels.Count);
    }

    public bool TryRemove(string clientKey, out VpnClientTunnel? clientTunnel)
    {
        var removed = _clientTunnels.TryRemove(clientKey, out clientTunnel);
        _trafficStats?.SetActiveConnections(_clientTunnels.Count);
        return removed;
    }

    public bool ContainsKey(string clientKey)
    {
        return _clientTunnels.ContainsKey(clientKey);
    }

    public bool TryGetValue(string clientKey, out VpnClientTunnel? clientTunnel)
    {
        return _clientTunnels.TryGetValue(clientKey, out clientTunnel);
    }

    public VpnClientTunnel this[string clientKey]
    {
        get
        {
            if (!_clientTunnels.TryGetValue(clientKey, out var clientTunnel))
            {
                throw new KeyNotFoundException($"Client tunnel not found for key: {clientKey}");
            }
            return clientTunnel;
        }
        set
        {
            _clientTunnels[clientKey] = value;
            _trafficStats?.SetActiveConnections(_clientTunnels.Count);
        }
    }

    public void Clear()
    {
        _clientTunnels.Clear();
        _trafficStats?.SetActiveConnections(0);
    }

    public IEnumerable<KeyValuePair<string, VpnClientTunnel>> GetAll()
    {
        return _clientTunnels;
    }

    public void CloseAll()
    {
        foreach (var clientEntry in _clientTunnels)
        {
            try
            {
                clientEntry.Value.Socket.Close();
            }
            catch { }
        }
        Clear();
    }

    private void StartIdleCheckTask()
    {
        if (_logger == null) return;

        try
        {
            _logger.LogInformation("Starting idle check task...");
            _idleCheckCts = new CancellationTokenSource();
            _ = RunIdleCheckTaskAsync(_idleCheckCts.Token);
            _logger.LogInformation("Idle check task started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start idle check task");
        }
    }

    private async Task RunIdleCheckTaskAsync(CancellationToken cancellationToken)
    {
        if (_logger == null) return;

        _logger.LogInformation("Idle check task running");
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(10000, cancellationToken);

                var idleClients = new List<string>();
                foreach (var (clientKey, tunnelClient) in _clientTunnels)
                {
                    if (tunnelClient.IsIdle())
                    {
                        idleClients.Add(clientKey);
                    }
                }

                if (idleClients.Count > 0)
                {
                    _logger.LogInformation("Closing {Count} idle clients", idleClients.Count);
                    foreach (var clientKey in idleClients)
                    {
                        if (TryRemove(clientKey, out var tunnelClient))
                        {
                            try
                            {
                                tunnelClient.CancellationTokenSource.Cancel();
                                tunnelClient.CancellationTokenSource.Dispose();

                                tunnelClient.Socket.Close();
                                tunnelClient.Socket.Dispose();
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Error closing idle client {ClientKey}", clientKey);
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Idle check task stopped");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in idle check task");
            }
        }
    }

    public void StopIdleCheckTask()
    {
        _idleCheckCts?.Cancel();
        _idleCheckCts?.Dispose();
        _idleCheckCts = null;
    }
}