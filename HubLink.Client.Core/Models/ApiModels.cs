namespace HubLink.Client.Models
{
    public class VpnStatusResponse
    {
        public bool IsConnected { get; set; }
        public string State { get; set; }
        public string? ErrorMessage { get; set; }
        public VpnServerInfo? Server { get; set; }
        public TrafficInfo Traffic { get; set; }
        public DateTime? ConnectedAt { get; set; }
        public double ConnectionDuration { get; set; } = 0;
        public int ReconnectAttempts { get; set; }
    }

    public class VpnServerInfo
    {
        public string Name { get; set; }
        public string ServerUrl { get; set; }
        public int LocalPort { get; set; }
        public bool EnableEncryption { get; set; }
        public bool AutoProxy { get; set; }
    }

    public class TrafficInfo
    {
        public long BytesSent { get; set; }
        public long BytesReceived { get; set; }
        public int ActiveConnections { get; set; }
        public double UploadSpeed { get; set; }
        public double DownloadSpeed { get; set; }
        public string Uptime { get; set; }
    }

    public class ServerListResponse
    {
        public List<VpnServerInfo> Servers { get; set; }
        public string? LastUsedServer { get; set; }
    }

    public class AddServerRequest
    {
        public string Name { get; set; }
        public string ServerUrl { get; set; }
        public int LocalPort { get; set; }
        public string EncryptionKey { get; set; }
        public bool EnableEncryption { get; set; }
        public bool AutoReconnect { get; set; }
        public int ReconnectInterval { get; set; }
        public bool AutoProxy { get; set; }
    }

    public class UpdateServerRequest
    {
        public string? Name { get; set; }
        public string? ServerUrl { get; set; }
        public int? LocalPort { get; set; }
        public string? EncryptionKey { get; set; }
        public bool? EnableEncryption { get; set; }
        public bool? AutoReconnect { get; set; }
        public int? ReconnectInterval { get; set; }
        public bool? AutoProxy { get; set; }
    }

    public class ConnectRequest
    {
        public string ServerName { get; set; }
    }

    public class ProxyStatusResponse
    {
        public bool IsEnabled { get; set; }
        public bool HttpEnabled { get; set; }
        public bool HttpsEnabled { get; set; }
        public bool SocksEnabled { get; set; }
        public string? HttpHost { get; set; }
        public int? HttpPort { get; set; }
        public string? HttpsHost { get; set; }
        public int? HttpsPort { get; set; }
        public string? SocksHost { get; set; }
        public int? SocksPort { get; set; }
        public string? PacUrl { get; set; }
    }

    public class EnableProxyRequest
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public bool AutoConfigure { get; set; }
    }

    public class ConfigurePacRequest
    {
        public string PacUrl { get; set; }
    }

    public class ConfigResponse
    {
        public List<VpnServerInfo> Servers { get; set; }
        public string? LastUsedServer { get; set; }
    }

    public class UpdateConfigRequest
    {
        public List<VpnServerInfo>? Servers { get; set; }
        public string? LastUsedServer { get; set; }
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; }
        public string Message { get; set; }
    }

    public class LogsResponse
    {
        public List<LogEntry> Logs { get; set; }
        public int TotalCount { get; set; }
    }

    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Message { get; set; }
        public string? ErrorCode { get; set; }

        public static ApiResponse<T> Ok(T data, string? message = null)
        {
            return new ApiResponse<T>
            {
                Success = true,
                Data = data,
                Message = message
            };
        }

        public static ApiResponse<T> Fail(string message, string? errorCode = null)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = message,
                ErrorCode = errorCode
            };
        }
    }

    public class SseEvent
    {
        public string Event { get; set; }
        public string Data { get; set; }
        public string Id { get; set; }
        public long Retry { get; set; }
    }

    public class ConfigInfo
    {
        public int ApiPort { get; set; }
        public string LogLevel { get; set; }
        public bool AutoReconnect { get; set; }
        public int ReconnectInterval { get; set; }
    }
}