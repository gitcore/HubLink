using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace HubLink.Shared;

public class TunnelChannel
{
    private readonly Channel<ReadOnlyMemory<byte>> _channel;
    private ChannelReader<ReadOnlyMemory<byte>>? _reader;
    private ChannelWriter<ReadOnlyMemory<byte>>? _writer;
    private readonly TunnelInfo _config;

    public TunnelChannel(int capacity = 1000, TunnelInfo? config = null)
    {
        _channel = Channel.CreateBounded<ReadOnlyMemory<byte>>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
        _config = config ?? new TunnelInfo();
    }

    public ChannelWriter<ReadOnlyMemory<byte>> Writer => _writer ?? _channel.Writer;
    public ChannelReader<ReadOnlyMemory<byte>> Reader => _reader ?? _channel.Reader;

    public void SetReader(ChannelReader<ReadOnlyMemory<byte>>? reader)
    {
        _reader = reader;
    }

    public void SetWriter(ChannelWriter<ReadOnlyMemory<byte>>? writer)
    {
        _writer = writer;
    }

    public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        await Writer.WriteAsync(data, cancellationToken);
    }

    public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(CancellationToken cancellationToken = default)
    {
        return await Reader.ReadAsync(cancellationToken);
    }

    public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var data in Reader.ReadAllAsync(cancellationToken))
        {
            yield return data;
        }
    }

    public async ValueTask<(string ClientKey, ReadOnlyMemory<byte> Payload)> ReadDecryptedPacketAsync(CancellationToken cancellationToken = default)
    {
        var encryptedPacket = await Reader.ReadAsync(cancellationToken);
        
        var decryptedPacket = _config.EnableEncryption && _config.EncryptionKey.Length > 0
            ? VpnPacketHelper.DecryptData(encryptedPacket, _config.EncryptionKey)
            : encryptedPacket;
        
        var (clientKey, payload) = VpnPacketHelper.ParseVpnPacket(decryptedPacket);
        
        return (clientKey, payload);
    }

    public async IAsyncEnumerable<(string clientKey, ReadOnlyMemory<byte> payload, ReadOnlyMemory<byte> encryptedPacket)> ReadAllDecryptedPacketsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var encryptedPacket in Reader.ReadAllAsync(cancellationToken))
        {
            var decryptedPacket = _config.EnableEncryption && _config.EncryptionKey.Length > 0
                ? VpnPacketHelper.DecryptData(encryptedPacket, _config.EncryptionKey)
                : encryptedPacket;
            
            var (clientKey, payload) = VpnPacketHelper.ParseVpnPacket(decryptedPacket);
            yield return (clientKey, payload, encryptedPacket);
        }
    }

    public void Complete()
    {
        _channel.Writer.Complete();
    }

    public bool TryComplete(Exception? exception = null)
    {
        return _channel.Writer.TryComplete(exception);
    }
}
