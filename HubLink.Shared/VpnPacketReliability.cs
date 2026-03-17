namespace HubLink.Shared;

public enum PacketType : byte
{
    Data = 0,
    Ack = 1
}

public class ReliablePacket
{
    public uint SequenceNumber { get; set; }
    public PacketType PacketType { get; set; }
    public uint AckNumber { get; set; }
    public byte[] Payload { get; set; } = [];
    public DateTime SendTime { get; set; } = DateTime.UtcNow;
    public int RetryCount { get; set; }
    public bool IsAcknowledged => RetryCount == -1;
}

public class PendingPacket
{
    public ReliablePacket Packet { get; set; }
    public DateTime FirstSendTime { get; set; }
    public DateTime LastSendTime { get; set; }
    public int RetryCount { get; set; }
    public Func<ReliablePacket, Task>? SendFunc { get; set; }

    public PendingPacket(ReliablePacket packet, Func<ReliablePacket, Task>? sendFunc = null)
    {
        Packet = packet;
        FirstSendTime = DateTime.UtcNow;
        LastSendTime = DateTime.UtcNow;
        RetryCount = 0;
        SendFunc = sendFunc;
    }
}

public class VpnPacketReliabilityOptions
{
    public int MaxRetryCount { get; set; } = 3;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(2);
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(5);
    public int WindowSize { get; set; } = 10;
}

public class VpnPacketReliability
{
    private readonly VpnPacketReliabilityOptions _options;
    private readonly ILogger? _logger;
    private readonly ConcurrentDictionary<uint, PendingPacket> _pendingPackets = new();
    private readonly ConcurrentQueue<ReliablePacket> _receiveBuffer = new();
    private uint _nextSequenceNumber = 1;
    private uint _expectedSequenceNumber = 1;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly SemaphoreSlim _receiveLock = new(1, 1);
    private readonly Timer _timeoutCheckTimer;
    private bool _disposed;

    public VpnPacketReliability(VpnPacketReliabilityOptions? options = null, ILogger? logger = null)
    {
        _options = options ?? new VpnPacketReliabilityOptions();
        _logger = logger;
        _timeoutCheckTimer = new Timer(CheckTimeouts, null, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));
    }

    public uint NextSequenceNumber => _nextSequenceNumber;

    public async Task<ReliablePacket> CreateDataPacketAsync(byte[] payload)
    {
        await _sendLock.WaitAsync();
        try
        {
            var packet = new ReliablePacket
            {
                SequenceNumber = _nextSequenceNumber++,
                PacketType = PacketType.Data,
                Payload = payload
            };
            return packet;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async Task<ReliablePacket> CreateAckPacketAsync(uint ackNumber)
    {
        return new ReliablePacket
        {
            SequenceNumber = 0,
            PacketType = PacketType.Ack,
            AckNumber = ackNumber
        };
    }

    public async Task<bool> SendPacketAsync(ReliablePacket packet, Func<ReliablePacket, Task> sendFunc)
    {
        if (packet.PacketType == PacketType.Data)
        {
            await _sendLock.WaitAsync();
            try
            {
                var pendingPacket = new PendingPacket(packet, sendFunc);
                _pendingPackets[packet.SequenceNumber] = pendingPacket;

                try
                {
                    await sendFunc(packet);
                    packet.SendTime = DateTime.UtcNow;
                    pendingPacket.LastSendTime = DateTime.UtcNow;
                    _logger?.LogInformation("Sent data packet {Seq}, pending: {Count}", packet.SequenceNumber, _pendingPackets.Count);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to send packet {Seq}", packet.SequenceNumber);
                    _pendingPackets.TryRemove(packet.SequenceNumber, out _);
                    return false;
                }
            }
            finally
            {
                _sendLock.Release();
            }
        }
        else
        {
            await sendFunc(packet);
            return true;
        }
    }

    public async Task ProcessPacketAsync(ReliablePacket packet, Func<ReliablePacket, Task> sendAckFunc)
    {
        if (packet.PacketType == PacketType.Data)
        {
            await _receiveLock.WaitAsync();
            try
            {
                var ackPacket = await CreateAckPacketAsync(packet.SequenceNumber);
                await sendAckFunc(ackPacket);
                _logger?.LogDebug("Sent ACK for packet {Seq}", packet.SequenceNumber);

                if (packet.SequenceNumber >= _expectedSequenceNumber)
                {
                    _receiveBuffer.Enqueue(packet);
                    _expectedSequenceNumber = packet.SequenceNumber + 1;
                    _logger?.LogDebug("Received data packet {Seq}, expected: {Expected}", packet.SequenceNumber, _expectedSequenceNumber);
                }
                else
                {
                    _logger?.LogDebug("Received duplicate packet {Seq}, expected: {Expected}", packet.SequenceNumber, _expectedSequenceNumber);
                }
            }
            finally
            {
                _receiveLock.Release();
            }
        }
        else if (packet.PacketType == PacketType.Ack)
        {
            await _sendLock.WaitAsync();
            try
            {
                if (_pendingPackets.TryRemove(packet.AckNumber, out var pendingPacket))
                {
                    var rtt = DateTime.UtcNow - pendingPacket.FirstSendTime;
                    _logger?.LogInformation("Received ACK for packet {Ack}, RTT: {Rtt}ms, retries: {Retries}", packet.AckNumber, rtt.TotalMilliseconds, pendingPacket.RetryCount);
                }
                else
                {
                    _logger?.LogWarning("Received ACK for unknown packet {Ack}, pending: {Count}", packet.AckNumber, _pendingPackets.Count);
                }
            }
            finally
            {
                _sendLock.Release();
            }
        }
    }

    public async Task<ReliablePacket?> ReceivePacketAsync(CancellationToken cancellationToken = default)
    {
        var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            while (!timeoutCts.Token.IsCancellationRequested)
            {
                if (_receiveBuffer.TryDequeue(out var packet))
                {
                    return packet;
                }
                await Task.Delay(10, timeoutCts.Token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            timeoutCts.Dispose();
        }
        return null;
    }

    private void CheckTimeouts(object? state)
    {
        if (_disposed) return;

        var now = DateTime.UtcNow;
        var packetsToRetry = new List<PendingPacket>();

        if (_sendLock.Wait(0))
        {
            try
            {
                foreach (var kvp in _pendingPackets)
                {
                    var pendingPacket = kvp.Value;
                    var elapsed = now - pendingPacket.LastSendTime;

                    if (elapsed >= _options.Timeout)
                    {
                        _logger?.LogWarning("Packet {Seq} timeout after {Elapsed}ms, retry count: {Retry}", 
                            pendingPacket.Packet.SequenceNumber, elapsed.TotalMilliseconds, pendingPacket.RetryCount);
                        packetsToRetry.Add(pendingPacket);
                    }
                }
            }
            finally
            {
                _sendLock.Release();
            }
        }

        foreach (var pendingPacket in packetsToRetry)
        {
            if (pendingPacket.RetryCount >= _options.MaxRetryCount)
            {
                _logger?.LogError("Packet {Seq} exceeded max retry count ({MaxRetries}), discarding", pendingPacket.Packet.SequenceNumber, _options.MaxRetryCount);
                _pendingPackets.TryRemove(pendingPacket.Packet.SequenceNumber, out _);
                continue;
            }

            pendingPacket.RetryCount++;
            pendingPacket.LastSendTime = now;

            var delay = TimeSpan.FromMilliseconds(Math.Min(
                (int)_options.Timeout.TotalMilliseconds * (int)Math.Pow(2, pendingPacket.RetryCount - 1),
                (int)_options.MaxRetryDelay.TotalMilliseconds
            ));

            _logger?.LogWarning("Retrying packet {Seq}, attempt {Retry}/{MaxRetries}, delay: {Delay}ms", 
                pendingPacket.Packet.SequenceNumber, pendingPacket.RetryCount, _options.MaxRetryCount, delay.TotalMilliseconds);

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delay);
                    if (!_pendingPackets.ContainsKey(pendingPacket.Packet.SequenceNumber))
                    {
                        _logger?.LogDebug("Packet {Seq} was ACKed during retry delay", pendingPacket.Packet.SequenceNumber);
                        return;
                    }

                    if (pendingPacket.SendFunc != null)
                    {
                        await pendingPacket.SendFunc(pendingPacket.Packet);
                        pendingPacket.Packet.SendTime = DateTime.UtcNow;
                        pendingPacket.LastSendTime = DateTime.UtcNow;
                        _logger?.LogWarning("Resent packet {Seq}, retry {Retry}", pendingPacket.Packet.SequenceNumber, pendingPacket.RetryCount);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to retry packet {Seq}", pendingPacket.Packet.SequenceNumber);
                }
            });
        }
    }

    public int GetPendingPacketCount()
    {
        return _pendingPackets.Count;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timeoutCheckTimer?.Dispose();
        _sendLock?.Dispose();
        _receiveLock?.Dispose();
    }
}
