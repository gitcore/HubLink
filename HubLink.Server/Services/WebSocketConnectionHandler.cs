using System.Net.WebSockets;
using System.Text.Json;

namespace HubLink.Server.Services;

public enum WebSocketMessageType : byte
{
    Connect = 0x01,
    Connected = 0x02,
    Disconnect = 0x03,
    StartTunnel = 0x04,
    StopTunnel = 0x05,
    OpenClientTunnelStream = 0x06,
    CloseClientTunnelStream = 0x07,
    TunnelData = 0x08,
    Heartbeat = 0x09,
    Error = 0x0A
}

public class WebSocketMessage
{
    public WebSocketMessageType Type { get; set; }
    public string? MethodName { get; set; }
    public object?[]? Args { get; set; }
    public string? ClientKey { get; set; }
    public string? Host { get; set; }
    public int? Port { get; set; }
    public byte[]? Data { get; set; }
    public string? ErrorMessage { get; set; }
}

public class WebSocketConnectionHandler
{
    private readonly VpnTunnelService _tunnelService;
    private readonly ILogger<WebSocketConnectionHandler> _logger;

    public WebSocketConnectionHandler(VpnTunnelService tunnelService, ILogger<WebSocketConnectionHandler> logger)
    {
        _tunnelService = tunnelService;
        _logger = logger;
    }

    public Task HandleConnectionAsync(WebSocket webSocket, HttpContext context)
    {
        throw new NotImplementedException("WebSocket transport is not implemented yet");
    }
}

public class WebSocketConnection
{
    private readonly WebSocket _webSocket;
    private readonly string _connectionId;
    private readonly ILogger _logger;

    public WebSocketConnection(WebSocket webSocket, string connectionId, ILogger logger)
    {
        _webSocket = webSocket;
        _connectionId = connectionId;
        _logger = logger;
    }

    public Task SendAsync(WebSocketMessage message)
    {
        throw new NotImplementedException("WebSocket transport is not implemented yet");
    }

    public Task ReceiveLoopAsync(Func<WebSocketMessage, Task> messageHandler)
    {
        throw new NotImplementedException("WebSocket transport is not implemented yet");
    }
}
