using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace HubLink.Server.Services;

public class TcpConnectionPool
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<PooledConnection>> _pool = new();
    private readonly ILogger<TcpConnectionPool> _logger;
    private readonly TimeSpan _maxIdleTime = TimeSpan.FromMinutes(5);
    private readonly int _maxPoolSizePerHost = 50;
    private readonly Timer _cleanupTimer;

    public TcpConnectionPool(ILogger<TcpConnectionPool> logger)
    {
        _logger = logger;
        _cleanupTimer = new Timer(CleanupIdleConnections, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public (bool, Socket) RentConnection(string key)
    {
        if (_pool.TryGetValue(key, out var queue))
        {
            while (queue.TryDequeue(out var pooled))
            {
                if (pooled?.Socket != null && pooled.Socket.Connected)
                {
                    _logger.LogDebug("Reusing connection to {Key}", key);
                    pooled.LastUsed = DateTime.Now;
                    return (false, pooled.Socket);
                }

                try
                {
                    pooled?.Socket.Close();
                    pooled?.Socket.Dispose();
                }
                catch { }
            }
        }

        _logger.LogDebug("Creating new connection to {Key} pool size: {Size}", key, queue?.Count ?? 0);

        var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
        socket.DualMode = true;
        
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 30);
        socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 5);
        socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 5);

        return (true, socket);
    }

    public void ReturnConnection(Socket socket)
    {
        var key = socket.RemoteEndPoint?.ToString() ?? string.Empty;

        if (string.IsNullOrEmpty(key) || !socket.Connected)
        {
            try
            {
                socket.Close();
                socket.Dispose();
            }
            catch { }

            _logger.LogWarning("Invalid {Key} for returning connection to pool", key);
            return;
        }

        if (!_pool.ContainsKey(key))
        {
            _pool[key] = new ConcurrentQueue<PooledConnection>();
        }

        var queue = _pool[key];

        if (queue.Count >= _maxPoolSizePerHost)
        {
            _logger.LogDebug("Pool full for {Key}, closing connection", key);
            try
            {
                socket.Close();
                socket.Dispose();
            }
            catch { }
            return;
        }

        queue.Enqueue(new PooledConnection
        {
            Socket = socket,
            CreatedAt = DateTime.Now,
            LastUsed = DateTime.Now
        });

        _logger.LogDebug("Returned connection to pool for {Key}, pool size: {Size}", key, queue.Count);
    }

    public void Clear()
    {
        foreach (var kvp in _pool)
        {
            while (kvp.Value.TryDequeue(out var pooled))
            {
                try
                {
                    pooled.Socket.Close();
                    pooled.Socket.Dispose();
                }
                catch { }
            }
        }

        _pool.Clear();
    }

    private void CleanupIdleConnections(object? state)
    {
        var now = DateTime.Now;
        var cleanedCount = 0;

        foreach (var kvp in _pool)
        {
            var queue = kvp.Value;
            var newQueue = new ConcurrentQueue<PooledConnection>();

            while (queue.TryDequeue(out var pooled))
            {
                if (now - pooled.LastUsed > _maxIdleTime)
                {
                    try
                    {
                        pooled.Socket.Close();
                        pooled.Socket.Dispose();
                        cleanedCount++;
                    }
                    catch { }
                }
                else if (pooled.Socket != null && pooled.Socket.Connected)
                {
                    newQueue.Enqueue(pooled);
                }
                else
                {
                    try
                    {
                        pooled.Socket.Close();
                        pooled.Socket.Dispose();
                        cleanedCount++;
                    }
                    catch { }
                }
            }

            _pool[kvp.Key] = newQueue;
        }

        if (cleanedCount > 0)
        {
            _logger.LogInformation("Cleaned up {Count} idle/dead connections", cleanedCount);
        }
    }

    private class PooledConnection
    {
        public Socket Socket { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime LastUsed { get; set; }
    }
}