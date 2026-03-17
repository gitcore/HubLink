using System.Data.Common;

namespace HubLink.Client.Services;

public class TunnelConnectionService : IAsyncDisposable
{
    private readonly ILogger<TunnelConnectionService> _logger;
    private readonly ITunnelTransport _transport;

    public event EventHandler<string>? OnConnected
    {
        add => _transport.OnConnected += value;
        remove => _transport.OnConnected -= value;
    }
    public event EventHandler<Exception>? OnClosed
    {
        add => _transport.OnClosed += value;
        remove => _transport.OnClosed -= value;
    }
    public event EventHandler<string>? OnEvent
    {
        add => _transport.OnEvent += value;
        remove => _transport.OnEvent -= value;
    }

    public bool IsConnected => _transport.IsConnected;
    public string? ConnectionId => _transport.ConnectionId;
    public CancellationToken ConnectionCancellationToken => _transport.ConnectionCancellationToken;

    public TunnelConnectionService(
        ILogger<TunnelConnectionService> logger,
        ITunnelTransport transport)
    {
        _logger = logger;
        _transport = transport;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _transport.ConnectAsync(cancellationToken);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _transport.DisconnectAsync(cancellationToken);
    }

    public async Task StartTunnelAsync(TunnelInfo config)
    {
        await _transport.StartTunnelAsync(config);
    }

    public async Task StopTunnelAsync()
    {
        await _transport.StopTunnelAsync();
    }

    public async Task<ChannelReader<ReadOnlyMemory<byte>>> OpenClientTunnelStreamAsync(string clientKey, VpnClientTunnel client)
    {
        return await _transport.OpenClientTunnelStreamAsync(clientKey, client);
    }

    public async Task CloseClientTunnelStreamAsync(string clientKey)
    {
        await _transport.CloseClientTunnelStreamAsync(clientKey);
    }

    public async Task InvokeAsync(string methodName)
    {
        await _transport.InvokeAsync(methodName);
    }

    public async Task InvokeAsync(string methodName, object args)
    {
        await _transport.InvokeAsync(methodName, args);
    }

    public async Task InvokeAsync(string methodName, object args1, object args2, object args3)
    {
        await _transport.InvokeAsync(methodName, args1, args2, args3);
    }

    public async Task<T> InvokeAsync<T>(string methodName, object args)
    {
        return await _transport.InvokeAsync<T>(methodName, args);
    }

    public async Task<ChannelReader<T>> StreamAsChannelAsync<T>(string methodName, ChannelReader<T> channel, CancellationToken cancellationToken = default)
    {
        return await _transport.StreamAsChannelAsync(methodName, channel, cancellationToken);
    }

    public async Task<ChannelReader<T>> StreamAsChannelAsync<T>(string methodName, object arg1, object arg2, object arg3, ChannelReader<T> channel, CancellationToken cancellationToken = default)
    {
        return await _transport.StreamAsChannelAsync(methodName, arg1, arg2, arg3, channel, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _transport.DisposeAsync();
    }
}
