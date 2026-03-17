namespace HubLink.Shared;

public enum TransportType
{
    WebSocket,
    SignalR
}

public interface ITunnelTransport : IAsyncDisposable
{
    TransportType TransportType { get; }
    bool IsConnected { get; }
    string? ConnectionId { get; }
    CancellationToken ConnectionCancellationToken { get; }

    event EventHandler<string>? OnConnected;
    event EventHandler<Exception>? OnClosed;
    event EventHandler<string>? OnEvent;

    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task StartTunnelAsync(TunnelInfo config);
    Task StopTunnelAsync();
    Task<ChannelReader<ReadOnlyMemory<byte>>> OpenClientTunnelStreamAsync(string clientKey, VpnClientTunnel client);
    Task CloseClientTunnelStreamAsync(string clientKey);
    Task InvokeAsync(string methodName);
    Task InvokeAsync(string methodName, object args);
    Task InvokeAsync(string methodName, object args1, object args2, object args3);
    Task<T> InvokeAsync<T>(string methodName, object args);
    Task<ChannelReader<T>> StreamAsChannelAsync<T>(string methodName, ChannelReader<T> channel, CancellationToken cancellationToken = default);
    Task<ChannelReader<T>> StreamAsChannelAsync<T>(string methodName, object arg1, object arg2, object arg3, ChannelReader<T> channel, CancellationToken cancellationToken = default);
}

public interface ITunnelTransportFactory
{
    ITunnelTransport CreateTransport(string serverUrl, string? apiKey = null, ILogger? logger = null);
}
