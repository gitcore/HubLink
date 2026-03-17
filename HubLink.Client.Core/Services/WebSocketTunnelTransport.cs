using System.Net.WebSockets;
using System.Text.Json;

namespace HubLink.Client.Services;

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

public class WebSocketTunnelTransport : ITunnelTransport
{
    private readonly ILogger<WebSocketTunnelTransport>? _logger;
    private readonly string _serverUrl;
    private readonly string _apiKey;

    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _connectionCts;
    private string? _connectionId;

    public TransportType TransportType => TransportType.WebSocket;
    public bool IsConnected => _webSocket?.State == WebSocketState.Open;
    public string? ConnectionId => _connectionId;
    public CancellationToken ConnectionCancellationToken => _connectionCts?.Token ?? CancellationToken.None;

    public event EventHandler<string>? OnConnected;
    public event EventHandler<Exception>? OnClosed;
    public event EventHandler<string>? OnEvent;

    public WebSocketTunnelTransport(
        string serverUrl,
        string? apiKey = null,
        ILogger<WebSocketTunnelTransport>? logger = null)
    {
        _serverUrl = serverUrl;
        _apiKey = apiKey ?? "your-secret-api-key-change-this-in-production";
        _logger = logger;
    }

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("WebSocket transport is not implemented yet");
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("WebSocket transport is not implemented yet");
    }

    public Task StartTunnelAsync(TunnelInfo config)
    {
        throw new NotImplementedException("WebSocket transport is not implemented yet");
    }

    public Task StopTunnelAsync()
    {
        throw new NotImplementedException("WebSocket transport is not implemented yet");
    }

    public Task<ChannelReader<ReadOnlyMemory<byte>>> OpenClientTunnelStreamAsync(string clientKey, VpnClientTunnel client)
    {
        throw new NotImplementedException("WebSocket transport is not implemented yet");
    }

    public Task CloseClientTunnelStreamAsync(string clientKey)
    {
        throw new NotImplementedException("WebSocket transport is not implemented yet");
    }

    public Task InvokeAsync(string methodName)
    {
        throw new NotImplementedException("WebSocket transport is not implemented yet");
    }

    public Task InvokeAsync(string methodName, object args)
    {
        throw new NotImplementedException("WebSocket transport is not implemented yet");
    }

    public Task InvokeAsync(string methodName, object args1, object args2, object args3)
    {
        throw new NotImplementedException("WebSocket transport is not implemented yet");
    }

    public Task<T> InvokeAsync<T>(string methodName, object args)
    {
        throw new NotImplementedException("WebSocket transport is not implemented yet");
    }

    public Task<ChannelReader<T>> StreamAsChannelAsync<T>(string methodName, ChannelReader<T> channel, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("WebSocket transport is not implemented yet");
    }

    public Task<ChannelReader<T>> StreamAsChannelAsync<T>(string methodName, object arg1, object arg2, object arg3, ChannelReader<T> channel, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("WebSocket transport is not implemented yet");
    }

    public async ValueTask DisposeAsync()
    {
        _webSocket?.Dispose();
        _webSocket = null;
        _connectionCts?.Dispose();
        _connectionCts = null;
    }
}

public class WebSocketTunnelTransportFactory : ITunnelTransportFactory
{
    public ITunnelTransport CreateTransport(string serverUrl, string? apiKey = null, ILogger? logger = null)
    {
        return new WebSocketTunnelTransport(serverUrl, apiKey, logger as ILogger<WebSocketTunnelTransport>);
    }
}
