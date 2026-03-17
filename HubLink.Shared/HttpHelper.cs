namespace HubLink.Shared
{
    public static class HttpHelper
    {
        public static bool IsTlsPacket(byte[] data)
        {
            try
            {
                if (data == null || data.Length < 5)
                    return false;

                byte contentType = data[0];
                byte majorVersion = data[1];
                byte minorVersion = data[2];

                bool validContentType = contentType == 0x14 || contentType == 0x15 || contentType == 0x16 || contentType == 0x17;
                bool validVersion = majorVersion == 0x03 && (minorVersion == 0x00 || minorVersion == 0x01 || minorVersion == 0x02 || minorVersion == 0x03 || minorVersion == 0x04);

                return validContentType && validVersion;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsConnectRequest(string requestText)
        {
            return requestText.StartsWith("CONNECT", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsHttpRequest(string requestText)
        {
            return requestText.StartsWith("GET", StringComparison.OrdinalIgnoreCase) ||
                   requestText.StartsWith("POST", StringComparison.OrdinalIgnoreCase) ||
                   requestText.StartsWith("PUT", StringComparison.OrdinalIgnoreCase) ||
                   requestText.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase) ||
                   requestText.StartsWith("HEAD", StringComparison.OrdinalIgnoreCase) ||
                   requestText.StartsWith("OPTIONS", StringComparison.OrdinalIgnoreCase) ||
                   requestText.StartsWith("PATCH", StringComparison.OrdinalIgnoreCase);
        }

        public static (string? host, int port) ParseHttpConnectRequest(string requestText)
        {
            var lines = requestText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) return (null, 0);

            var firstLine = lines[0];
            var parts = firstLine.Split(' ');
            if (parts.Length < 2) return (null, 0);

            var hostPort = parts[1];
            var colonIndex = hostPort.IndexOf(':');
            if (colonIndex > 0)
            {
                var host = hostPort.Substring(0, colonIndex);
                var portStr = hostPort.Substring(colonIndex + 1);
                if (int.TryParse(portStr, out int port))
                {
                    return (host, port);
                }
            }

            return (null, 0);
        }

        public static (string? host, int port) ParseHttpRequest(string requestText)
        {
            var lines = requestText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) return (null, 0);

            foreach (var line in lines)
            {
                if (line.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
                {
                    var hostHeader = line.Substring("Host:".Length).Trim();
                    var colonIndex = hostHeader.IndexOf(':');
                    if (colonIndex > 0)
                    {
                        var host = hostHeader.Substring(0, colonIndex);
                        var portStr = hostHeader.Substring(colonIndex + 1);
                        if (int.TryParse(portStr, out int port))
                        {
                            return (host, port);
                        }
                    }
                    return (hostHeader, 80);
                }
            }

            return (null, 0);
        }

        public static bool IsConnectRequest(ReadOnlyMemory<byte> data)
        {
            try
            {
                if (data.Length < 8)
                    return false;

                var requestText = Encoding.ASCII.GetString(data.Span.Slice(0, Math.Min(data.Length, 8192)));
                var connectMatch = Regex.Match(
                    requestText,
                    @"CONNECT\s+([^\s:]+):(\d+)",
                    RegexOptions.IgnoreCase
                );

                return connectMatch.Success;
            }
            catch
            {
                return false;
            }
        }

        public static (string address, int port) ParseHttpHost(ReadOnlyMemory<byte> data)
        {
            try
            {
                if (data.Length == 0)
                    return ("www.baidu.com", 80);

                var requestText = Encoding.ASCII.GetString(data.Span.Slice(0, Math.Min(data.Length, 8192)));

                var connectMatch = Regex.Match(
                    requestText,
                    @"CONNECT\s+([^\s:]+):(\d+)",
                    RegexOptions.IgnoreCase
                );

                if (connectMatch.Success)
                {
                    var host = connectMatch.Groups[1].Value.Trim();
                    var port = int.Parse(connectMatch.Groups[2].Value);
                    return (host, port);
                }

                var hostMatch = Regex.Match(
                    requestText,
                    @"Host:\s*([^\r\n:]+)(?::(\d+))?",
                    RegexOptions.IgnoreCase
                );

                if (hostMatch.Success)
                {
                    var host = hostMatch.Groups[1].Value.Trim();
                    var port = hostMatch.Groups[2].Success
                        ? int.Parse(hostMatch.Groups[2].Value)
                        : 80;
                    return (host, port);
                }

                return ("www.baidu.com", 80);
            }
            catch (Exception)
            {
                return ("www.baidu.com", 80);
            }
        }
    }
}