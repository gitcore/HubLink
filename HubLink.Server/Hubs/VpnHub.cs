namespace HubLink.Server.Hubs;

public class VpnHub(VpnTunnelService tunnelService, ILogger<VpnHub> logger) : Hub
{
    private static readonly ConcurrentDictionary<string, string> _connectedClients = new();
    private readonly VpnTunnelService _tunnelService = tunnelService;
    private readonly ILogger<VpnHub> _logger = logger;

    public override async Task OnConnectedAsync()
    {
        var clientId = Context.ConnectionId;
        var userAgent = Context.GetHttpContext()?.Request.Headers["User-Agent"].ToString() ?? "Unknown";
        _connectedClients[clientId] = userAgent;
        _logger.LogInformation("Client connected: {ClientId} (User-Agent: {UserAgent})", clientId, userAgent);
        await Clients.Caller.SendAsync("Connected", clientId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var clientId = Context.ConnectionId;
        _connectedClients.TryRemove(clientId, out _);
        _tunnelService.StopTunnelAsync(clientId);
        _logger.LogInformation("Client disconnected: {ClientId} (Reason: {Reason})", clientId, exception?.Message ?? "Unknown");
        await base.OnDisconnectedAsync(exception);
    }

    [HubMethodName("StartTunnel")]
    public async Task StartTunnelAsync(TunnelInfo config)
    {
        var clientId = Context.ConnectionId;
        
        if (_tunnelService.TunnelExists(clientId))
        {
            _logger.LogInformation("Tunnel already exists for {ClientId}, closing old tunnel", clientId);
            _tunnelService.StopTunnelAsync(clientId);
        }
        
        _tunnelService.StartTunnelAsync(clientId, config);
        await Clients.Caller.SendAsync("VpnHub", "VpnConnected");
    }

    [HubMethodName("StopTunnel")]
    public async Task StopTunnelAsync()
    {
        _tunnelService.StopTunnelAsync(Context.ConnectionId);
        await Clients.Caller.SendAsync("VpnHub", "VpnDisconnected");
    }

    [HubMethodName("StreamAsChannel")]
    public Task<ChannelReader<ReadOnlyMemory<byte>>> StreamAsChannelAsync(ChannelReader<ReadOnlyMemory<byte>> clientTunnelReader)
    {
        throw new NotImplementedException();
    }

    [HubMethodName("OpenClientTunnelStream")]
    public async Task<ChannelReader<ReadOnlyMemory<byte>>> OpenClientTunnelStreamAsync(string clientKey, string host, int port, ChannelReader<ReadOnlyMemory<byte>> clientTunnelReader)
    {
        var tunnelReader = await _tunnelService.OpenClientTunnelStreamAsync(Context.ConnectionId, clientKey, host, port, clientTunnelReader);
        await Clients.Caller.SendAsync("VpnHub", "ClientTunnelStreamOpened"); 
        return tunnelReader;
    }

    [HubMethodName("CloseClientTunnelStream")]
    public async Task CloseClientTunnelStreamAsync(string clientKey)
    {
        _tunnelService.CloseClientTunnelStreamAsync(clientKey);
        await Clients.Caller.SendAsync("VpnHub", "ClientTunnelStreamClosed"); 
    }

    public ConcurrentDictionary<string, string> GetConnectedClients()
    {
        return _connectedClients;
    }
}