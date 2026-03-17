namespace HubLink.Shared;

public class VpnClientTunnel(string clientKey, CancellationTokenSource cancellationTokenSource, TunnelInfo tunnelInfo, ILogger? logger = null, ITrafficStats? trafficStats = null)
{
    public Socket? Socket { get; private set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 0;
    public CancellationTokenSource CancellationTokenSource { get; set; } = cancellationTokenSource;
    public string TunnelId { get; set; } = string.Empty;
    public string ClientKey => clientKey;

    private readonly Channel<ReadOnlyMemory<byte>> _remoteChannel = Channel.CreateBounded<ReadOnlyMemory<byte>>(new BoundedChannelOptions(1000)
    {
        FullMode = BoundedChannelFullMode.DropOldest
    });

    private ChannelReader<ReadOnlyMemory<byte>>? _reader;
    private ChannelWriter<ReadOnlyMemory<byte>>? _writer;
    private DateTime _lastActivityTime = DateTime.UtcNow;
    private readonly VpnPacketReliability _reliability = new VpnPacketReliability(new VpnPacketReliabilityOptions
    {
        MaxRetryCount = 3,
        Timeout = TimeSpan.FromSeconds(5),
        MaxRetryDelay = TimeSpan.FromSeconds(10)
    }, logger);

    private Task? readTask;
    private Task? writeTask;

    public ChannelWriter<ReadOnlyMemory<byte>> Writer => _writer ?? _remoteChannel.Writer;
    public ChannelReader<ReadOnlyMemory<byte>> Reader => _reader ?? _remoteChannel.Reader;

    public event Func<Task>? OnClosed;

    private void NotifyClosed()
    {
        OnClosed?.Invoke();
    }

    protected virtual async Task ReadFromTargetAsync()
    {
        logger?.LogDebug("[{ClientKey}] ReadFromTargetAsync: Starting to read from target", ClientKey);
        var buffer = ArrayPool<byte>.Shared.Rent(65536);
        int totalBytesRead = 0;

        var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationTokenSource.Token);

        try
        {
            int bytesRead = 0;
            while (Socket != null && (bytesRead = await Socket.ReceiveAsync(new ArraySegment<byte>(buffer, 0, buffer.Length), SocketFlags.None, cts.Token)) > 0)
            {
                totalBytesRead += bytesRead;
                _lastActivityTime = DateTime.UtcNow;
                
                var responseData = new ReadOnlyMemory<byte>(buffer, 0, bytesRead);

                await WriteEncryptedPacketAsync(ClientKey, responseData, cts.Token);
                trafficStats?.AddReceived(responseData.Length);
                logger?.LogDebug("[{ClientKey}] ReadFromTargetAsync: read {Bytes} bytes from target", ClientKey, responseData.Length);
            }
        }
        catch (OperationCanceledException)
        {
            logger?.LogDebug("[{ClientKey}] ReadFromTargetAsync canceled", Host);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "[{ClientKey}] Read from target error", ClientKey);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            Close();
        }
    }

    protected virtual async Task WriteToTargetAsync()
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationTokenSource.Token);

        try
        {
            logger?.LogDebug("[{ClientKey}] WriteToTargetAsync: Starting to write to target", ClientKey);
            await foreach (var (packetKey, payload) in ReadAllDecryptedPacketsAsync(cts.Token))
            {
                try
                {
                    if (Socket != null)
                    {
                        await Socket.SendAsync(payload, SocketFlags.None, cts.Token);
                    }
                    trafficStats?.AddSent(payload.Length);
                    logger?.LogDebug("[{ClientKey}] WriteToTargetAsync: sent {Bytes} bytes to target", ClientKey, payload.Length);
                }
                catch (ObjectDisposedException)
                {
                    logger?.LogWarning("[{ClientKey}] Client connection closed", ClientKey);
                    break;
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "[{ClientKey}] Error writing to client stream", ClientKey);
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger?.LogDebug("[{ClientKey}] WriteToTargetAsync canceled", ClientKey);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "[{ClientKey}] Error processing data to target stream", ClientKey);
        }
        finally
        {
            logger?.LogDebug("[{ClientKey}] closed: {TargetAddress}:{TargetPort}", ClientKey, Host, Port);
        }
    }

    protected virtual async Task ReadFromClientAsync()
    {
        var buffer = ArrayPool<byte>.Shared.Rent(65535);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationTokenSource.Token);

        try
        {
            logger?.LogDebug("[{ClientKey}] ReadFromClientAsync: Starting to read from client", ClientKey);
            int bytesRead;
            int totalBytesRead = 0;
            while (Socket != null && (bytesRead = await Socket.ReceiveAsync(new ArraySegment<byte>(buffer, 0, buffer.Length), SocketFlags.None, cts.Token)) > 0)
            {
                totalBytesRead += bytesRead;
                _lastActivityTime = DateTime.UtcNow;
                var data = new ReadOnlyMemory<byte>(buffer, 0, bytesRead);

                try
                {
                    await WriteEncryptedPacketAsync(clientKey, data, cts.Token);
                    trafficStats?.AddSent(data.Length);
                    logger?.LogDebug("[{ClientKey}] Read {Bytes} bytes from client, total: {Total}", clientKey, bytesRead, totalBytesRead);
                }
                catch (OperationCanceledException)
                {
                    logger?.LogWarning("[{ClientKey}] Channel write canceled", clientKey);
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger?.LogDebug("[{ClientKey}] ReadFromClientAsync canceled", ClientKey);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "[{ClientKey}] Client connection error", ClientKey);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            Close();
        }
    }

    protected virtual async Task WriteToClientAsync(Func<Task<ChannelReader<ReadOnlyMemory<byte>>>> readerFactory)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationTokenSource.Token);

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                var tunnelReader = await readerFactory();

                SetTunnelReader(tunnelReader);

                logger?.LogDebug("[{ClientKey}] WriteToClientAsync: starting to write to client", ClientKey);
                await foreach (var (_, data) in ReadAllDecryptedPacketsAsync(cts.Token))
                {
                    try
                    {
                        if (Socket != null)
                        {
                            await Socket.SendAsync(data, SocketFlags.None, cts.Token);
                        }
                        trafficStats?.AddReceived(data.Length);
                    }
                    catch (OperationCanceledException)
                    {
                        logger?.LogWarning("[{ClientKey}] writing canceled", clientKey);
                        break;
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "[{ClientKey}] Error writing to client stream", clientKey);
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                logger?.LogDebug("[{ClientKey}] Data channel processing canceled", clientKey);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "[{ClientKey}] Error processing data channel", clientKey);
            }

            await Task.Delay(1000, cts.Token);
            logger?.LogInformation("[{ClientKey}] reconnect to server", ClientKey);
        }
    }

    public void RefreshIdleTime()
    {
        _lastActivityTime = DateTime.UtcNow;
    }

    public bool IsIdle()
    {
        var idleTime = DateTime.UtcNow - _lastActivityTime;
        return idleTime.TotalSeconds >= tunnelInfo.IdleTimeoutSeconds;
    }

    public double GetIdleSeconds()
    {
        return (DateTime.UtcNow - _lastActivityTime).TotalSeconds;
    }

    public void SetTunnelReader(ChannelReader<ReadOnlyMemory<byte>>? reader)
    {
        _reader = reader;
    }

    public void SetTunnelWriter(ChannelWriter<ReadOnlyMemory<byte>>? writer)
    {
        _writer = writer;
    }

    public async Task AcceptAsync(Socket socket,
        Func<Task<ChannelReader<ReadOnlyMemory<byte>>>> readerFactory)
    {
        Socket = socket;

        socket.NoDelay = true;

        var handshakeBuffer = ArrayPool<byte>.Shared.Rent(8192);
        var totalBytesRead = 0;

        try
        {
            int bytesRead;
            while ((bytesRead = await socket.ReceiveAsync(new ArraySegment<byte>(handshakeBuffer, totalBytesRead, handshakeBuffer.Length - totalBytesRead), SocketFlags.None, CancellationTokenSource.Token)) > 0)
            {
                totalBytesRead += bytesRead;
                var data = new ReadOnlyMemory<byte>(handshakeBuffer, 0, totalBytesRead);
                logger?.LogDebug("AcceptAsync: received {Bytes} bytes, first byte: 0x{FirstByte:X2}", totalBytesRead, data.Span[0]);

                if (Socks5Helper.IsSocks5Handshake(data))
                {
                    if (totalBytesRead >= 3)
                    {
                        var (version, methods) = Socks5Helper.ParseSocks5Handshake(data);
                        logger?.LogDebug("SOCKS5 handshake received, version: {Version}, methods: {MethodCount}", version, methods.Length);

                        byte selectedMethod;
                        if (Socks5Helper.IsNoAuthRequired(methods))
                        {
                            selectedMethod = Socks5Helper.NO_AUTH;
                            logger?.LogDebug("Selected method: No authentication");
                        }
                        else
                        {
                            selectedMethod = Socks5Helper.NO_ACCEPTABLE_METHOD;
                            logger?.LogWarning("No acceptable method found");
                        }

                        if (selectedMethod == Socks5Helper.NO_ACCEPTABLE_METHOD)
                        {
                            socket.Close();
                            socket.Dispose();
                            Socket = null;
                            NotifyClosed();
                            return;
                        }

                        var methodResponse = Socks5Helper.CreateSocks5HandshakeResponse(selectedMethod);
                        await socket.SendAsync(methodResponse, SocketFlags.None, CancellationTokenSource.Token);
                        logger?.LogDebug("Sent SOCKS5 method selection response");

                        totalBytesRead = 0;
                        while ((bytesRead = await socket.ReceiveAsync(new ArraySegment<byte>(handshakeBuffer, totalBytesRead, handshakeBuffer.Length - totalBytesRead), SocketFlags.None, CancellationTokenSource.Token)) > 0)
                        {
                            totalBytesRead += bytesRead;
                            data = new ReadOnlyMemory<byte>(handshakeBuffer, 0, totalBytesRead);

                            try
                            {
                                var (reqVersion, cmd, atyp, host, port) = Socks5Helper.ParseSocks5Request(data);
                                logger?.LogDebug("SOCKS5 request: cmd={Cmd}, host={Host}, port={Port}", cmd, host, port);

                                if (Socks5Helper.IsConnectCommand(cmd))
                                {
                                    Host = host;
                                    Port = port;
                                    logger?.LogInformation("SOCKS5 CONNECT: {Host}:{Port}", host, port);

                                    var successResponse = Socks5Helper.CreateSocks5Response(Socks5Helper.REP_SUCCESS);
                                    await socket.SendAsync(successResponse, SocketFlags.None, CancellationTokenSource.Token);
                                    logger?.LogDebug("Sent SOCKS5 success response for {Host}:{Port}", host, port);

                                    var requestLength = Socks5Helper.GetRequestLength(data);
                                    if (totalBytesRead > requestLength)
                                    {
                                        var extraData = data.Slice(requestLength);
                                        logger?.LogDebug("Forwarding {ExtraBytes} extra bytes after SOCKS5 request", extraData.Length);
                                        try
                                        {
                                            await WriteEncryptedPacketAsync(clientKey, extraData, CancellationTokenSource.Token);
                                        }
                                        catch (OperationCanceledException)
                                        {
                                            logger?.LogWarning("[{ClientKey}] Channel write canceled", clientKey);
                                        }
                                    }

                                    break;
                                }
                                else
                                {
                                    logger?.LogWarning("Unsupported SOCKS5 command: {Cmd}", cmd);
                                    var errorResponse = Socks5Helper.CreateSocks5Response(Socks5Helper.REP_COMMAND_NOT_SUPPORTED);
                                    await socket.SendAsync(errorResponse, SocketFlags.None, CancellationTokenSource.Token);
                                    socket.Close();
                                    socket.Dispose();
                                    Socket = null;
                                    NotifyClosed();
                                    return;
                                }
                            }
                            catch (ArgumentException)
                            {
                                if (totalBytesRead >= handshakeBuffer.Length)
                                {
                                    logger?.LogWarning("SOCKS5 request too large");
                                    socket.Close();
                                    socket.Dispose();
                                    Socket = null;
                                    NotifyClosed();
                                    return;
                                }
                                continue;
                            }
                        }
                        break;
                    }
                }
                else
                {
                    var requestText = Encoding.ASCII.GetString(data.Span.Slice(0, Math.Min(data.Length, 8192)));
                    logger?.LogDebug("Received HTTP request: {Request}", requestText.Trim());

                    if (HttpHelper.IsConnectRequest(requestText))
                    {
                        var (host, port) = HttpHelper.ParseHttpConnectRequest(requestText);
                        if (host != null && port > 0)
                        {
                            Host = host;
                            Port = port;
                            logger?.LogInformation("HTTP CONNECT: {Host}:{Port}", host, port);

                            var connectResponse = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n");
                            await socket.SendAsync(connectResponse, SocketFlags.None, CancellationTokenSource.Token);
                            logger?.LogDebug("Sent HTTP 200 response to client for CONNECT {Host}:{Port}", host, port);

                            break;
                        }
                        else
                        {
                            socket.Close();
                            socket.Dispose();
                            Socket = null;
                            NotifyClosed();
                            return;
                        }
                    }
                    else if (HttpHelper.IsHttpRequest(requestText))
                    {
                        var (host, port) = HttpHelper.ParseHttpRequest(requestText);
                        if (host != null && port > 0)
                        {
                            Host = host;
                            Port = port;
                            logger?.LogInformation("AcceptAsync: HTTP request for {Host}:{Port}", host, port);
                            try
                            {
                                await WriteEncryptedPacketAsync(clientKey, data, CancellationTokenSource.Token);
                                logger?.LogDebug("AcceptAsync: sent {Bytes} bytes of HTTP request to server", data.Length);
                            }
                            catch (OperationCanceledException)
                            {
                                logger?.LogWarning("[{ClientKey}] Channel write canceled", clientKey);
                            }

                            break;
                        }
                        else
                        {
                            socket.Close();
                            socket.Dispose();
                            Socket = null;
                            NotifyClosed();
                            return;
                        }
                    }
                    else
                    {
                        if (totalBytesRead >= handshakeBuffer.Length)
                        {
                            logger?.LogWarning("Unsupported request type, buffer full");
                            socket.Close();
                            socket.Dispose();
                            Socket = null;
                            NotifyClosed();
                            return;
                        }
                    }
                }
            }

            writeTask = WriteToClientAsync(readerFactory);
            readTask = ReadFromClientAsync();

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.WhenAll(readTask, writeTask);
                }
                catch
                {
                }
                finally
                {
                    NotifyClosed();
                }
            });
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "AcceptAsync: handshake error");
            socket.Close();
            socket.Dispose();
            Socket = null;
            NotifyClosed();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(handshakeBuffer);
        }
    }

    public async Task ConnectAsync(IPEndPoint endPoint)
    {
        var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
        socket.NoDelay = true;
        socket.DualMode = true;

        Socket = socket;

        await socket.ConnectAsync(endPoint);
        logger?.LogInformation("[{TunnelKey}] connected to {TargetAddress}:{TargetPort} -> {EndPoint}", clientKey, Host, Port, endPoint);

        readTask = ReadFromTargetAsync();
        writeTask = WriteToTargetAsync();

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.WhenAll(readTask, writeTask);
            }
            catch
            {
            }
            finally
            {
                NotifyClosed();
            }
        });
    }

    public async Task WriteEncryptedPacketAsync(string clientKey, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        try
        {
            RefreshIdleTime();
            
            if (tunnelInfo.EnableReliablePackets)
            {
                var packet = await _reliability.CreateDataPacketAsync(data.ToArray());
                
                await _reliability.SendPacketAsync(packet, async (p) =>
                {
                    var packetData = VpnPacketHelper.CreateReliablePacket(p, clientKey);
                    var encryptedPacket = tunnelInfo.EnableEncryption
                        ? VpnPacketHelper.EncryptData(packetData, tunnelInfo.EncryptionKey)
                        : packetData;
                    await Writer.WriteAsync(encryptedPacket, cancellationToken);
                });
            }
            else
            {
                var packet = VpnPacketHelper.CreateVpnPacket(data, clientKey);
                var encryptedPacket = tunnelInfo.EnableEncryption ? VpnPacketHelper.EncryptData(packet, tunnelInfo.EncryptionKey) : packet;
                await Writer.WriteAsync(encryptedPacket, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    public async Task WriteRawAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        try
        {
            RefreshIdleTime();
            await Writer.WriteAsync(data, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    public async ValueTask<(string ClientKey, ReadOnlyMemory<byte> Payload)> ReadDecryptedPacketAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var encryptedPacket = await Reader.ReadAsync(cancellationToken);
            RefreshIdleTime();

            var decryptedPacket = tunnelInfo.EnableEncryption
                ? VpnPacketHelper.DecryptData(encryptedPacket, tunnelInfo.EncryptionKey)
                : encryptedPacket;

            if (tunnelInfo.EnableReliablePackets)
            {
                var (clientKey, packet) = VpnPacketHelper.ParseReliablePacket(decryptedPacket);
                
                if (packet.PacketType == PacketType.Data)
                {
                    await _reliability.ProcessPacketAsync(packet, async (ackPacket) =>
                    {
                        var ackData = VpnPacketHelper.CreateReliablePacket(ackPacket, clientKey);
                        var ackEncrypted = tunnelInfo.EnableEncryption
                            ? VpnPacketHelper.EncryptData(ackData, tunnelInfo.EncryptionKey)
                            : ackData;
                        await Writer.WriteAsync(ackEncrypted, cancellationToken);
                    });
                    return (clientKey, packet.Payload);
                }
                else if (packet.PacketType == PacketType.Ack)
                {
                    await _reliability.ProcessPacketAsync(packet, async (_) => { });
                    return (string.Empty, ReadOnlyMemory<byte>.Empty);
                }
                else
                {
                    return (clientKey, packet.Payload);
                }
            }
            else
            {
                var (packetClientKey, payload) = VpnPacketHelper.ParseVpnPacket(decryptedPacket);
                return (packetClientKey, payload);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    public async ValueTask<ReadOnlyMemory<byte>> ReadRawAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var data = await Reader.ReadAsync(cancellationToken);
            RefreshIdleTime();
            return data;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    public async IAsyncEnumerable<(string clientKey, ReadOnlyMemory<byte> payload)> ReadAllDecryptedPacketsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (tunnelInfo.EnableReliablePackets)
        {
            await foreach (var encryptedPacket in Reader.ReadAllAsync(cancellationToken))
            {
                RefreshIdleTime();
                var decryptedPacket = tunnelInfo.EnableEncryption
                    ? VpnPacketHelper.DecryptData(encryptedPacket, tunnelInfo.EncryptionKey)
                    : encryptedPacket;

                var (clientKey, packet) = VpnPacketHelper.ParseReliablePacket(decryptedPacket);
                if (packet.PacketType == PacketType.Data)
                {
                    await _reliability.ProcessPacketAsync(packet, async (ackPacket) =>
                    {
                        var ackData = VpnPacketHelper.CreateReliablePacket(ackPacket, clientKey);
                        var ackEncrypted = tunnelInfo.EnableEncryption
                            ? VpnPacketHelper.EncryptData(ackData, tunnelInfo.EncryptionKey)
                            : ackData;
                        await Writer.WriteAsync(ackEncrypted, cancellationToken);
                    });
                    yield return (clientKey, packet.Payload);
                }
                else if (packet.PacketType == PacketType.Ack)
                {
                    await _reliability.ProcessPacketAsync(packet, async (_) => { });
                }
            }
        }
        else
        {
            await foreach (var encryptedPacket in Reader.ReadAllAsync(cancellationToken))
            {
                RefreshIdleTime();
                var decryptedPacket = tunnelInfo.EnableEncryption
                    ? VpnPacketHelper.DecryptData(encryptedPacket, tunnelInfo.EncryptionKey)
                    : encryptedPacket;

                var (clientKey, payload) = VpnPacketHelper.ParseVpnPacket(decryptedPacket);
                yield return (clientKey, payload);
            }
        }
    }

    public void Close()
    {
        try
        {
            CancellationTokenSource.Cancel();

            _writer?.Complete();

            _remoteChannel.Writer.Complete();
        }
        catch { }

        try
        {
            _reliability?.Dispose();
        }
        catch { }

        try
        {
            Socket?.Shutdown(SocketShutdown.Both);
            Socket?.Close();
            Socket?.Dispose();
            Socket = null;

            CancellationTokenSource.Dispose();
        }
        catch { }
    }
}
