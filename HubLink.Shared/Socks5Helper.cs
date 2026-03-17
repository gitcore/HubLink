namespace HubLink.Shared;

public class Socks5Helper
{
    public const byte SOCKS_VERSION = 0x05;
    public const byte NO_AUTH = 0x00;
    public const byte NO_ACCEPTABLE_METHOD = 0xFF;
    
    public const byte CMD_CONNECT = 0x01;
    public const byte CMD_BIND = 0x02;
    public const byte CMD_UDP_ASSOCIATE = 0x03;
    
    public const byte ATYP_IPV4 = 0x01;
    public const byte ATYP_DOMAIN = 0x03;
    public const byte ATYP_IPV6 = 0x04;
    
    public const byte REP_SUCCESS = 0x00;
    public const byte REP_GENERAL_FAILURE = 0x01;
    public const byte REP_CONNECTION_NOT_ALLOWED = 0x02;
    public const byte REP_NETWORK_UNREACHABLE = 0x03;
    public const byte REP_HOST_UNREACHABLE = 0x04;
    public const byte REP_CONNECTION_REFUSED = 0x05;
    public const byte REP_TTL_EXPIRED = 0x06;
    public const byte REP_COMMAND_NOT_SUPPORTED = 0x07;
    public const byte REP_ADDRESS_TYPE_NOT_SUPPORTED = 0x08;

    public static bool IsSocks5Handshake(ReadOnlyMemory<byte> data)
    {
        if (data.Length < 3)
            return false;
        
        return data.Span[0] == SOCKS_VERSION;
    }

    public static (byte version, byte[] methods) ParseSocks5Handshake(ReadOnlyMemory<byte> data)
    {
        if (data.Length < 3)
            throw new ArgumentException("Invalid SOCKS5 handshake data");
        
        byte version = data.Span[0];
        byte methodCount = data.Span[1];
        byte[] methods = new byte[methodCount];
        data.Span.Slice(2, methodCount).CopyTo(methods);
        
        return (version, methods);
    }

    public static byte[] CreateSocks5HandshakeResponse(byte selectedMethod)
    {
        return new byte[] { SOCKS_VERSION, selectedMethod };
    }

    public static (byte version, byte cmd, byte atyp, string host, int port) ParseSocks5Request(ReadOnlyMemory<byte> data)
    {
        if (data.Length < 10)
            throw new ArgumentException("Invalid SOCKS5 request data");
        
        var span = data.Span;
        byte version = span[0];
        byte cmd = span[1];
        byte atyp = span[3];
        
        string host;
        int port;
        int offset = 4;
        
        switch (atyp)
        {
            case ATYP_IPV4:
                var ipv4 = new IPAddress(span.Slice(offset, 4).ToArray());
                host = ipv4.ToString();
                offset += 4;
                break;
                
            case ATYP_DOMAIN:
                byte domainLen = span[offset];
                offset++;
                host = Encoding.ASCII.GetString(span.Slice(offset, domainLen));
                offset += domainLen;
                break;
                
            case ATYP_IPV6:
                var ipv6 = new IPAddress(span.Slice(offset, 16).ToArray());
                host = ipv6.ToString();
                offset += 16;
                break;
                
            default:
                throw new ArgumentException($"Unsupported address type: {atyp}");
        }
        
        port = (span[offset] << 8) | span[offset + 1];
        
        return (version, cmd, atyp, host, port);
    }

    public static byte[] CreateSocks5Response(byte replyCode, string bindAddress = "0.0.0.0", int bindPort = 0)
    {
        var response = new List<byte> { SOCKS_VERSION, replyCode, 0x00, ATYP_IPV4 };
        
        var bindIp = IPAddress.Parse(bindAddress);
        response.AddRange(bindIp.GetAddressBytes());
        
        response.Add((byte)(bindPort >> 8));
        response.Add((byte)(bindPort & 0xFF));
        
        return response.ToArray();
    }

    public static bool IsConnectCommand(byte cmd)
    {
        return cmd == CMD_CONNECT;
    }

    public static bool IsBindCommand(byte cmd)
    {
        return cmd == CMD_BIND;
    }

    public static bool IsUdpAssociateCommand(byte cmd)
    {
        return cmd == CMD_UDP_ASSOCIATE;
    }

    public static bool IsNoAuthRequired(byte[] methods)
    {
        return methods.Contains(NO_AUTH);
    }

    public static string GetReplyCodeDescription(byte replyCode)
    {
        return replyCode switch
        {
            REP_SUCCESS => "Success",
            REP_GENERAL_FAILURE => "General SOCKS server failure",
            REP_CONNECTION_NOT_ALLOWED => "Connection not allowed by ruleset",
            REP_NETWORK_UNREACHABLE => "Network unreachable",
            REP_HOST_UNREACHABLE => "Host unreachable",
            REP_CONNECTION_REFUSED => "Connection refused",
            REP_TTL_EXPIRED => "TTL expired",
            REP_COMMAND_NOT_SUPPORTED => "Command not supported",
            REP_ADDRESS_TYPE_NOT_SUPPORTED => "Address type not supported",
            _ => "Unknown error"
        };
    }

    public static int GetRequestLength(ReadOnlyMemory<byte> data)
    {
        if (data.Length < 10)
            throw new ArgumentException("Invalid SOCKS5 request data");
        
        var span = data.Span;
        byte atyp = span[3];
        
        int offset = 4;
        
        switch (atyp)
        {
            case ATYP_IPV4:
                offset += 4;
                break;
                
            case ATYP_DOMAIN:
                byte domainLen = span[offset];
                offset += 1 + domainLen;
                break;
                
            case ATYP_IPV6:
                offset += 16;
                break;
                
            default:
                throw new ArgumentException($"Unsupported address type: {atyp}");
        }
        
        offset += 2;
        
        return offset;
    }
}