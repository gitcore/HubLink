using Xunit;
using System.Collections.Concurrent;

namespace HubLink.Test;

public class VpnReliablePacketTest
{
    [Fact]
    public void TestCreateReliablePacket()
    {
        var packet = new ReliablePacket
        {
            SequenceNumber = 1,
            PacketType = PacketType.Data,
            AckNumber = 0,
            Payload = Encoding.UTF8.GetBytes("Test payload")
        };
        
        var clientKey = "test-client-123";
        var data = VpnPacketHelper.CreateReliablePacket(packet, clientKey);
        
        Assert.True(data.Length > packet.Payload.Length);
    }

    [Fact]
    public void TestParseReliablePacket()
    {
        var originalPacket = new ReliablePacket
        {
            SequenceNumber = 42,
            PacketType = PacketType.Data,
            AckNumber = 0,
            Payload = Encoding.UTF8.GetBytes("Hello World")
        };
        
        var clientKey = "test-client-456";
        var data = VpnPacketHelper.CreateReliablePacket(originalPacket, clientKey);
        
        var (parsedClientKey, parsedPacket) = VpnPacketHelper.ParseReliablePacket(data);
        
        Assert.Equal(clientKey, parsedClientKey);
        Assert.Equal(originalPacket.SequenceNumber, parsedPacket.SequenceNumber);
        Assert.Equal(originalPacket.PacketType, parsedPacket.PacketType);
        Assert.Equal(originalPacket.AckNumber, parsedPacket.AckNumber);
        Assert.Equal(originalPacket.Payload, parsedPacket.Payload);
    }

    [Fact]
    public void TestCreateAckPacket()
    {
        var packet = new ReliablePacket
        {
            SequenceNumber = 0,
            PacketType = PacketType.Ack,
            AckNumber = 123,
            Payload = Array.Empty<byte>()
        };
        
        var clientKey = "test-client-789";
        var data = VpnPacketHelper.CreateReliablePacket(packet, clientKey);
        
        var (parsedClientKey, parsedPacket) = VpnPacketHelper.ParseReliablePacket(data);
        
        Assert.Equal(clientKey, parsedClientKey);
        Assert.Equal(PacketType.Ack, parsedPacket.PacketType);
        Assert.Equal(123u, parsedPacket.AckNumber);
        Assert.Empty(parsedPacket.Payload);
    }

    [Fact]
    public void TestLargePayloadPacket()
    {
        var largePayload = new byte[65536];
        for (int i = 0; i < largePayload.Length; i++)
        {
            largePayload[i] = (byte)(i % 256);
        }
        
        var packet = new ReliablePacket
        {
            SequenceNumber = 999,
            PacketType = PacketType.Data,
            AckNumber = 0,
            Payload = largePayload
        };
        
        var clientKey = "test-client-large";
        var data = VpnPacketHelper.CreateReliablePacket(packet, clientKey);
        
        var (parsedClientKey, parsedPacket) = VpnPacketHelper.ParseReliablePacket(data);
        
        Assert.Equal(clientKey, parsedClientKey);
        Assert.Equal(999u, parsedPacket.SequenceNumber);
        Assert.Equal(PacketType.Data, parsedPacket.PacketType);
        Assert.Equal(largePayload.Length, parsedPacket.Payload.Length);
        Assert.Equal(largePayload, parsedPacket.Payload);
    }

    [Fact]
    public void TestEmptyPayloadPacket()
    {
        var packet = new ReliablePacket
        {
            SequenceNumber = 1,
            PacketType = PacketType.Data,
            AckNumber = 0,
            Payload = Array.Empty<byte>()
        };
        
        var clientKey = "test-client-empty";
        var data = VpnPacketHelper.CreateReliablePacket(packet, clientKey);
        
        var (parsedClientKey, parsedPacket) = VpnPacketHelper.ParseReliablePacket(data);
        
        Assert.Equal(clientKey, parsedClientKey);
        Assert.Equal(1u, parsedPacket.SequenceNumber);
        Assert.Equal(PacketType.Data, parsedPacket.PacketType);
        Assert.Empty(parsedPacket.Payload);
    }

    [Fact]
    public void TestSequenceNumberEncoding()
    {
        var testCases = new[] { 0u, 1u, 255u, 256u, 65535u, 65536u, 16777215u, 16777216u, uint.MaxValue };
        
        foreach (var seqNum in testCases)
        {
            var packet = new ReliablePacket
            {
                SequenceNumber = seqNum,
                PacketType = PacketType.Data,
                AckNumber = 0,
                Payload = Encoding.UTF8.GetBytes($"Seq {seqNum}")
            };
            
            var clientKey = $"client-{seqNum}";
            var data = VpnPacketHelper.CreateReliablePacket(packet, clientKey);
            
            var (_, parsedPacket) = VpnPacketHelper.ParseReliablePacket(data);
            
            Assert.Equal(seqNum, parsedPacket.SequenceNumber);
        }
    }

    [Fact]
    public void TestAckNumberEncoding()
    {
        var testCases = new[] { 0u, 1u, 255u, 256u, 65535u, 65536u, 16777215u, 16777216u, uint.MaxValue };
        
        foreach (var ackNum in testCases)
        {
            var packet = new ReliablePacket
            {
                SequenceNumber = 0,
                PacketType = PacketType.Ack,
                AckNumber = ackNum,
                Payload = Array.Empty<byte>()
            };
            
            var clientKey = $"client-ack-{ackNum}";
            var data = VpnPacketHelper.CreateReliablePacket(packet, clientKey);
            
            var (_, parsedPacket) = VpnPacketHelper.ParseReliablePacket(data);
            
            Assert.Equal(ackNum, parsedPacket.AckNumber);
        }
    }

    [Fact]
    public void TestPacketTypeEncoding()
    {
        var packetTypes = new[] { PacketType.Data, PacketType.Ack };
        
        foreach (var packetType in packetTypes)
        {
            var packet = new ReliablePacket
            {
                SequenceNumber = 1,
                PacketType = packetType,
                AckNumber = 0,
                Payload = Encoding.UTF8.GetBytes($"Type {packetType}")
            };
            
            var clientKey = $"client-type-{packetType}";
            var data = VpnPacketHelper.CreateReliablePacket(packet, clientKey);
            
            var (_, parsedPacket) = VpnPacketHelper.ParseReliablePacket(data);
            
            Assert.Equal(packetType, parsedPacket.PacketType);
        }
    }

    [Fact]
    public void TestMultiplePackets()
    {
        var packets = new List<ReliablePacket>();
        var clientKey = "multi-packet-client";
        
        for (int i = 1; i <= 10; i++)
        {
            var packet = new ReliablePacket
            {
                SequenceNumber = (uint)i,
                PacketType = PacketType.Data,
                AckNumber = 0,
                Payload = Encoding.UTF8.GetBytes($"Packet {i}")
            };
            
            var data = VpnPacketHelper.CreateReliablePacket(packet, clientKey);
            var (_, parsedPacket) = VpnPacketHelper.ParseReliablePacket(data);
            
            packets.Add(parsedPacket);
        }
        
        Assert.Equal(10, packets.Count);
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal((uint)(i + 1), packets[i].SequenceNumber);
            Assert.Equal(PacketType.Data, packets[i].PacketType);
            Assert.Equal(Encoding.UTF8.GetBytes($"Packet {i + 1}"), packets[i].Payload);
        }
    }

    [Fact]
    public void TestLongClientKey()
    {
        var longClientKey = new string('a', 1000);
        
        var packet = new ReliablePacket
        {
            SequenceNumber = 1,
            PacketType = PacketType.Data,
            AckNumber = 0,
            Payload = Encoding.UTF8.GetBytes("Long client key test")
        };
        
        var data = VpnPacketHelper.CreateReliablePacket(packet, longClientKey);
        
        var (parsedClientKey, parsedPacket) = VpnPacketHelper.ParseReliablePacket(data);
        
        Assert.Equal(longClientKey, parsedClientKey);
        Assert.Equal(1u, parsedPacket.SequenceNumber);
        Assert.Equal(PacketType.Data, parsedPacket.PacketType);
        Assert.Equal(Encoding.UTF8.GetBytes("Long client key test"), parsedPacket.Payload);
    }

    [Fact]
    public void TestSpecialCharactersInPayload()
    {
        var specialPayload = new byte[] { 0x00, 0x01, 0x02, 0xFF, 0xFE, 0xFD, 0x80, 0x7F };
        
        var packet = new ReliablePacket
        {
            SequenceNumber = 1,
            PacketType = PacketType.Data,
            AckNumber = 0,
            Payload = specialPayload
        };
        
        var clientKey = "special-chars-client";
        var data = VpnPacketHelper.CreateReliablePacket(packet, clientKey);
        
        var (_, parsedPacket) = VpnPacketHelper.ParseReliablePacket(data);
        
        Assert.Equal(specialPayload, parsedPacket.Payload);
    }

    [Fact]
    public void TestPacketWithEncryption()
    {
        var encryptionKey = "MySecretKey123";
        var keyBytes = VpnPacketHelper.GetEncryptionKey(encryptionKey);
        
        var packet = new ReliablePacket
        {
            SequenceNumber = 42,
            PacketType = PacketType.Data,
            AckNumber = 0,
            Payload = Encoding.UTF8.GetBytes("Encrypted packet test")
        };
        
        var clientKey = "encrypted-client";
        var packetData = VpnPacketHelper.CreateReliablePacket(packet, clientKey);
        var encryptedData = VpnPacketHelper.EncryptData(packetData, keyBytes);
        var decryptedData = VpnPacketHelper.DecryptData(encryptedData, keyBytes);
        
        var (parsedClientKey, parsedPacket) = VpnPacketHelper.ParseReliablePacket(decryptedData);
        
        Assert.Equal(clientKey, parsedClientKey);
        Assert.Equal(42u, parsedPacket.SequenceNumber);
        Assert.Equal(PacketType.Data, parsedPacket.PacketType);
        Assert.Equal(Encoding.UTF8.GetBytes("Encrypted packet test"), parsedPacket.Payload);
    }

    [Fact]
    public void TestInvalidPacketParsing()
    {
        var emptyData = Array.Empty<byte>();
        var (clientKey1, packet1) = VpnPacketHelper.ParseReliablePacket(emptyData);
        
        Assert.Equal(string.Empty, clientKey1);
        Assert.Equal(0u, packet1.SequenceNumber);
        
        var shortData = new byte[5];
        var (clientKey2, packet2) = VpnPacketHelper.ParseReliablePacket(shortData);
        
        Assert.Equal(string.Empty, clientKey2);
        Assert.Equal(0u, packet2.SequenceNumber);
    }

    [Fact]
    public void TestPacketRoundTrip()
    {
        var originalPacket = new ReliablePacket
        {
            SequenceNumber = 12345,
            PacketType = PacketType.Data,
            AckNumber = 0,
            Payload = Encoding.UTF8.GetBytes("Round trip test payload with some data")
        };
        
        var clientKey = "round-trip-client";
        
        var data = VpnPacketHelper.CreateReliablePacket(originalPacket, clientKey);
        var (parsedClientKey, parsedPacket) = VpnPacketHelper.ParseReliablePacket(data);
        
        Assert.Equal(clientKey, parsedClientKey);
        Assert.Equal(originalPacket.SequenceNumber, parsedPacket.SequenceNumber);
        Assert.Equal(originalPacket.PacketType, parsedPacket.PacketType);
        Assert.Equal(originalPacket.AckNumber, parsedPacket.AckNumber);
        Assert.Equal(originalPacket.Payload, parsedPacket.Payload);
    }

    [Fact]
    public void TestConcurrentPacketCreationAndParsing()
    {
        var tasks = new List<Task>();
        var errors = new ConcurrentBag<Exception>();
        
        for (int i = 0; i < 100; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    var packet = new ReliablePacket
                    {
                        SequenceNumber = (uint)index,
                        PacketType = PacketType.Data,
                        AckNumber = 0,
                        Payload = Encoding.UTF8.GetBytes($"Concurrent test {index}")
                    };
                    
                    var clientKey = $"concurrent-client-{index}";
                    var data = VpnPacketHelper.CreateReliablePacket(packet, clientKey);
                    var (parsedClientKey, parsedPacket) = VpnPacketHelper.ParseReliablePacket(data);
                    
                    Assert.Equal(clientKey, parsedClientKey);
                    Assert.Equal((uint)index, parsedPacket.SequenceNumber);
                    Assert.Equal(PacketType.Data, parsedPacket.PacketType);
                    Assert.Equal(Encoding.UTF8.GetBytes($"Concurrent test {index}"), parsedPacket.Payload);
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            }));
        }
        
        Task.WaitAll(tasks.ToArray());
        
        Assert.Empty(errors);
    }
}
