namespace HubLink.Shared;

public static class VpnPacketHelper
{
    public static byte[] GetEncryptionKey(string key)
    {
        return Encoding.UTF8.GetBytes(key.PadRight(32).Substring(0, 32));
    }

    public static ReadOnlyMemory<byte> EncryptData(ReadOnlyMemory<byte> data, byte[] key)
    {
        var aes = AesCryptoPool.Instance.RentEncryptor(key);
        try
        {
            using var encryptor = aes.CreateEncryptor();
            var encrypted = encryptor.TransformFinalBlock(data.ToArray(), 0, data.Length);
            return encrypted;
        }
        finally
        {
            AesCryptoPool.Instance.ReturnEncryptor(aes, key);
        }
    }

    public static ReadOnlyMemory<byte> DecryptData(ReadOnlyMemory<byte> data, byte[] key)
    {
        var aes = AesCryptoPool.Instance.RentDecryptor(key);
        try
        {
            using var decryptor = aes.CreateDecryptor();
            var decrypted = decryptor.TransformFinalBlock(data.ToArray(), 0, data.Length);
            return decrypted;
        }
        finally
        {
            AesCryptoPool.Instance.ReturnDecryptor(aes, key);
        }
    }

    public static ReadOnlyMemory<byte> CreateVpnPacket(byte[] data, string clientKey)
    {
        return CreateVpnPacket(data.AsMemory(), clientKey);
    }

    public static ReadOnlyMemory<byte> CreateVpnPacket(ReadOnlyMemory<byte> data, string clientKey)
    {
        var clientKeyBytes = Encoding.UTF8.GetBytes(clientKey);
        
        var totalLength = 2 + clientKeyBytes.Length + data.Length;
        var packet = new byte[totalLength];
        
        var offset = 0;
        
        packet[offset++] = (byte)(clientKeyBytes.Length >> 8);
        packet[offset++] = (byte)clientKeyBytes.Length;
        
        clientKeyBytes.CopyTo(packet, offset);
        offset += clientKeyBytes.Length;
        
        data.Span.CopyTo(packet.AsSpan(offset));

        return packet;
    }

    public static (string clientKey, ReadOnlyMemory<byte> payload) ParseVpnPacket(byte[] data)
    {
        return ParseVpnPacket(data.AsMemory());
    }

    public static (string clientKey, ReadOnlyMemory<byte> payload) ParseVpnPacket(ReadOnlyMemory<byte> data)
    {
        var span = data.Span;
        
        if (span.Length < 2)
            return (string.Empty, ReadOnlyMemory<byte>.Empty);

        var offset = 0;
        var clientKeyLength = (span[offset] << 8) | span[offset + 1];
        offset += 2;
        
        if (span.Length < offset + clientKeyLength)
            return (string.Empty, ReadOnlyMemory<byte>.Empty);

        var clientKey = Encoding.UTF8.GetString(span.Slice(offset, clientKeyLength));
        offset += clientKeyLength;

        var payload = data.Slice(offset);
        return (clientKey, payload);
    }

    public static ReadOnlyMemory<byte> CreateReliablePacket(ReliablePacket packet, string clientKey)
    {
        var clientKeyBytes = Encoding.UTF8.GetBytes(clientKey);
        var headerLength = 2 + clientKeyBytes.Length + 12;
        var totalLength = headerLength + packet.Payload.Length;
        var buffer = new byte[totalLength];
        
        var offset = 0;
        
        buffer[offset++] = (byte)(clientKeyBytes.Length >> 8);
        buffer[offset++] = (byte)clientKeyBytes.Length;
        
        clientKeyBytes.CopyTo(buffer, offset);
        offset += clientKeyBytes.Length;
        
        buffer[offset++] = (byte)(packet.SequenceNumber >> 24);
        buffer[offset++] = (byte)(packet.SequenceNumber >> 16);
        buffer[offset++] = (byte)(packet.SequenceNumber >> 8);
        buffer[offset++] = (byte)packet.SequenceNumber;
        
        buffer[offset++] = (byte)packet.PacketType;
        
        buffer[offset++] = (byte)(packet.AckNumber >> 24);
        buffer[offset++] = (byte)(packet.AckNumber >> 16);
        buffer[offset++] = (byte)(packet.AckNumber >> 8);
        buffer[offset++] = (byte)packet.AckNumber;
        
        buffer[offset++] = 0;
        buffer[offset++] = 0;
        buffer[offset++] = 0;
        
        if (packet.Payload.Length > 0)
        {
            packet.Payload.CopyTo(buffer, offset);
        }

        return buffer;
    }

    public static (string clientKey, ReliablePacket packet) ParseReliablePacket(ReadOnlyMemory<byte> data)
    {
        var span = data.Span;
        
        if (span.Length < 2)
            return (string.Empty, new ReliablePacket());

        var offset = 0;
        var clientKeyLength = (span[offset] << 8) | span[offset + 1];
        offset += 2;
        
        if (span.Length < offset + clientKeyLength + 12)
            return (string.Empty, new ReliablePacket());

        var clientKey = Encoding.UTF8.GetString(span.Slice(offset, clientKeyLength));
        offset += clientKeyLength;
        
        var sequenceNumber = (uint)((span[offset] << 24) | (span[offset + 1] << 16) | (span[offset + 2] << 8) | span[offset + 3]);
        offset += 4;
        
        var packetType = (PacketType)span[offset];
        offset += 1;
        
        var ackNumber = (uint)((span[offset] << 24) | (span[offset + 1] << 16) | (span[offset + 2] << 8) | span[offset + 3]);
        offset += 4;
        
        offset += 3;
        
        var payload = data.Slice(offset);
        
        var packet = new ReliablePacket
        {
            SequenceNumber = sequenceNumber,
            PacketType = packetType,
            AckNumber = ackNumber,
            Payload = payload.ToArray()
        };

        return (clientKey, packet);
    }
}
