using Xunit;

namespace HubLink.Test;

public class VpnPacketReliabilityTest
{
    [Fact]
    public async Task TestCreateDataPacket()
    {
        var reliability = new VpnPacketReliability();
        var payload = Encoding.UTF8.GetBytes("Test payload data");
        
        var packet = await reliability.CreateDataPacketAsync(payload);
        
        Assert.Equal(PacketType.Data, packet.PacketType);
        Assert.Equal(payload, packet.Payload);
        Assert.Equal(1u, packet.SequenceNumber);
        
        reliability.Dispose();
    }

    [Fact]
    public async Task TestCreateAckPacket()
    {
        var reliability = new VpnPacketReliability();
        var ackNumber = 5u;
        
        var packet = await reliability.CreateAckPacketAsync(ackNumber);
        
        Assert.Equal(PacketType.Ack, packet.PacketType);
        Assert.Equal(ackNumber, packet.AckNumber);
        Assert.Equal(0u, packet.SequenceNumber);
        
        reliability.Dispose();
    }

    [Fact]
    public async Task TestSendAndReceivePacket()
    {
        var senderReliability = new VpnPacketReliability();
        var receiverReliability = new VpnPacketReliability();
        
        var sentPackets = new List<ReliablePacket>();
        var receivedPackets = new List<ReliablePacket>();
        var ackPackets = new List<ReliablePacket>();
        
        var payload = Encoding.UTF8.GetBytes("Hello World");
        var packet = await senderReliability.CreateDataPacketAsync(payload);
        
        var sendTask = senderReliability.SendPacketAsync(packet, async (p) =>
        {
            sentPackets.Add(p);
        });
        
        var receiveTask = Task.Run(async () =>
        {
            await receiverReliability.ProcessPacketAsync(packet, async (ack) =>
            {
                ackPackets.Add(ack);
                await senderReliability.ProcessPacketAsync(ack, async (_) => { });
            });
            
            var receivedPacket = await receiverReliability.ReceivePacketAsync();
            if (receivedPacket != null)
            {
                receivedPackets.Add(receivedPacket);
            }
        });
        
        await Task.WhenAll(sendTask, receiveTask);
        
        Assert.Single(receivedPackets);
        Assert.Single(ackPackets);
        Assert.Single(sentPackets);
        Assert.Equal(payload, receivedPackets[0].Payload);
        Assert.Equal(1u, receivedPackets[0].SequenceNumber);
        Assert.Equal(1u, ackPackets[0].AckNumber);
        
        senderReliability.Dispose();
        receiverReliability.Dispose();
    }

    [Fact]
    public async Task TestPacketSequenceNumbers()
    {
        var reliability = new VpnPacketReliability();
        
        var packet1 = await reliability.CreateDataPacketAsync(Encoding.UTF8.GetBytes("Packet 1"));
        var packet2 = await reliability.CreateDataPacketAsync(Encoding.UTF8.GetBytes("Packet 2"));
        var packet3 = await reliability.CreateDataPacketAsync(Encoding.UTF8.GetBytes("Packet 3"));
        
        Assert.Equal(1u, packet1.SequenceNumber);
        Assert.Equal(2u, packet2.SequenceNumber);
        Assert.Equal(3u, packet3.SequenceNumber);
        
        reliability.Dispose();
    }

    [Fact]
    public async Task TestDuplicatePacketHandling()
    {
        var senderReliability = new VpnPacketReliability();
        var receiverReliability = new VpnPacketReliability();
        
        var payload = Encoding.UTF8.GetBytes("Test data");
        var packet = await senderReliability.CreateDataPacketAsync(payload);
        
        var sendTask = senderReliability.SendPacketAsync(packet, async (p) => { });
        
        var receiveTask = Task.Run(async () =>
        {
            await receiverReliability.ProcessPacketAsync(packet, async (ack) =>
            {
                await senderReliability.ProcessPacketAsync(ack, async (_) => { });
            });
            
            await receiverReliability.ProcessPacketAsync(packet, async (ack) =>
            {
                await senderReliability.ProcessPacketAsync(ack, async (_) => { });
            });
            
            var receivedPacket1 = await receiverReliability.ReceivePacketAsync();
            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            var receivedPacket2 = await receiverReliability.ReceivePacketAsync(cts.Token);
            
            Assert.NotNull(receivedPacket1);
            Assert.Null(receivedPacket2);
        });
        
        await Task.WhenAll(sendTask, receiveTask);
        
        senderReliability.Dispose();
        receiverReliability.Dispose();
    }

    [Fact]
    public async Task TestPacketTimeoutAndRetry()
    {
        var options = new VpnPacketReliabilityOptions
        {
            MaxRetryCount = 3,
            Timeout = TimeSpan.FromMilliseconds(500)
        };
        
        var reliability = new VpnPacketReliability(options);
        var sendAttempts = 0;
        
        var payload = Encoding.UTF8.GetBytes("Test timeout");
        var packet = await reliability.CreateDataPacketAsync(payload);
        
        await reliability.SendPacketAsync(packet, async (p) =>
        {
            sendAttempts++;
            if (sendAttempts < 3)
            {
                await Task.Delay(1000);
            }
        });
        
        await Task.Delay(2000);
        
        Assert.True(sendAttempts >= 2);
        
        reliability.Dispose();
    }

    [Fact]
    public async Task TestMaxRetryExceeded()
    {
        var options = new VpnPacketReliabilityOptions
        {
            MaxRetryCount = 2,
            Timeout = TimeSpan.FromMilliseconds(100)
        };
        
        var reliability = new VpnPacketReliability(options);
        var sendAttempts = 0;
        
        var payload = Encoding.UTF8.GetBytes("Test max retry");
        var packet = await reliability.CreateDataPacketAsync(payload);
        
        await reliability.SendPacketAsync(packet, async (p) =>
        {
            sendAttempts++;
        });
        
        await Task.Delay(1000);
        
        Assert.Equal(3, sendAttempts);
        Assert.Equal(0, reliability.GetPendingPacketCount());
        
        reliability.Dispose();
    }

    [Fact]
    public async Task TestMultiplePacketsInFlight()
    {
        var senderReliability = new VpnPacketReliability();
        var receiverReliability = new VpnPacketReliability();
        
        var packets = new List<ReliablePacket>();
        for (int i = 0; i < 5; i++)
        {
            var payload = Encoding.UTF8.GetBytes($"Packet {i}");
            var packet = await senderReliability.CreateDataPacketAsync(payload);
            packets.Add(packet);
        }
        
        var sendTasks = packets.Select(async p =>
        {
            await senderReliability.SendPacketAsync(p, async (packet) =>
            {
                await receiverReliability.ProcessPacketAsync(packet, async (ack) =>
                {
                    await senderReliability.ProcessPacketAsync(ack, async (_) => { });
                });
            });
        });
        
        await Task.WhenAll(sendTasks);
        
        var receivedPackets = new List<ReliablePacket>();
        for (int i = 0; i < 5; i++)
        {
            var packet = await receiverReliability.ReceivePacketAsync();
            if (packet != null)
            {
                receivedPackets.Add(packet);
            }
        }
        
        Assert.Equal(5, receivedPackets.Count);
        
        senderReliability.Dispose();
        receiverReliability.Dispose();
    }

    [Fact]
    public async Task TestConcurrentPacketSending()
    {
        var senderReliability = new VpnPacketReliability();
        var receiverReliability = new VpnPacketReliability();
        
        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                var payload = Encoding.UTF8.GetBytes($"Concurrent packet {index}");
                var packet = await senderReliability.CreateDataPacketAsync(payload);
                
                await senderReliability.SendPacketAsync(packet, async (p) =>
                {
                    await receiverReliability.ProcessPacketAsync(p, async (ack) =>
                    {
                        await senderReliability.ProcessPacketAsync(ack, async (_) => { });
                    });
                });
            }));
        }
        
        await Task.WhenAll(tasks);
        
        var receivedCount = 0;
        for (int i = 0; i < 10; i++)
        {
            var packet = await receiverReliability.ReceivePacketAsync();
            if (packet != null)
            {
                receivedCount++;
            }
        }
        
        Assert.Equal(10, receivedCount);
        
        senderReliability.Dispose();
        receiverReliability.Dispose();
    }

    [Fact]
    public async Task TestAckProcessing()
    {
        var senderReliability = new VpnPacketReliability();
        
        var payload = Encoding.UTF8.GetBytes("Test ACK");
        var packet = await senderReliability.CreateDataPacketAsync(payload);
        
        await senderReliability.SendPacketAsync(packet, async (p) => { });
        
        Assert.Equal(1, senderReliability.GetPendingPacketCount());
        
        var ackPacket = await senderReliability.CreateAckPacketAsync(packet.SequenceNumber);
        await senderReliability.ProcessPacketAsync(ackPacket, async (_) => { });
        
        Assert.Equal(0, senderReliability.GetPendingPacketCount());
        
        senderReliability.Dispose();
    }

    [Fact]
    public async Task TestPendingPacketCount()
    {
        var reliability = new VpnPacketReliability();
        
        var packet1 = await reliability.CreateDataPacketAsync(Encoding.UTF8.GetBytes("Packet 1"));
        var packet2 = await reliability.CreateDataPacketAsync(Encoding.UTF8.GetBytes("Packet 2"));
        var packet3 = await reliability.CreateDataPacketAsync(Encoding.UTF8.GetBytes("Packet 3"));
        
        await reliability.SendPacketAsync(packet1, async (p) => { });
        await reliability.SendPacketAsync(packet2, async (p) => { });
        await reliability.SendPacketAsync(packet3, async (p) => { });
        
        Assert.Equal(3, reliability.GetPendingPacketCount());
        
        var ack1 = await reliability.CreateAckPacketAsync(packet1.SequenceNumber);
        await reliability.ProcessPacketAsync(ack1, async (_) => { });
        
        Assert.Equal(2, reliability.GetPendingPacketCount());
        
        reliability.Dispose();
    }

    [Fact]
    public async Task TestLargePayload()
    {
        var senderReliability = new VpnPacketReliability();
        var receiverReliability = new VpnPacketReliability();
        
        var largePayload = new byte[65536];
        for (int i = 0; i < largePayload.Length; i++)
        {
            largePayload[i] = (byte)(i % 256);
        }
        
        var packet = await senderReliability.CreateDataPacketAsync(largePayload);
        
        await senderReliability.SendPacketAsync(packet, async (p) =>
        {
            await receiverReliability.ProcessPacketAsync(p, async (ack) =>
            {
                await senderReliability.ProcessPacketAsync(ack, async (_) => { });
            });
        });
        
        var receivedPacket = await receiverReliability.ReceivePacketAsync();
        
        Assert.NotNull(receivedPacket);
        Assert.Equal(largePayload.Length, receivedPacket.Payload.Length);
        Assert.Equal(largePayload, receivedPacket.Payload);
        
        senderReliability.Dispose();
        receiverReliability.Dispose();
    }

    [Fact]
    public async Task TestPacketOrdering()
    {
        var senderReliability = new VpnPacketReliability();
        var receiverReliability = new VpnPacketReliability();
        
        var packets = new List<ReliablePacket>();
        for (int i = 0; i < 3; i++)
        {
            var payload = Encoding.UTF8.GetBytes($"Packet {i}");
            var packet = await senderReliability.CreateDataPacketAsync(payload);
            packets.Add(packet);
        }
        
        await senderReliability.SendPacketAsync(packets[0], async (p) =>
        {
            await receiverReliability.ProcessPacketAsync(p, async (ack) =>
            {
                await senderReliability.ProcessPacketAsync(ack, async (_) => { });
            });
        });
        
        await senderReliability.SendPacketAsync(packets[1], async (p) =>
        {
            await receiverReliability.ProcessPacketAsync(p, async (ack) =>
            {
                await senderReliability.ProcessPacketAsync(ack, async (_) => { });
            });
        });
        
        await senderReliability.SendPacketAsync(packets[2], async (p) =>
        {
            await receiverReliability.ProcessPacketAsync(p, async (ack) =>
            {
                await senderReliability.ProcessPacketAsync(ack, async (_) => { });
            });
        });
        
        var receivedPackets = new List<ReliablePacket>();
        for (int i = 0; i < 3; i++)
        {
            var packet = await receiverReliability.ReceivePacketAsync();
            if (packet != null)
            {
                receivedPackets.Add(packet);
            }
        }
        
        Assert.Equal(3, receivedPackets.Count);
        Assert.Equal(1u, receivedPackets[0].SequenceNumber);
        Assert.Equal(2u, receivedPackets[1].SequenceNumber);
        Assert.Equal(3u, receivedPackets[2].SequenceNumber);
        
        senderReliability.Dispose();
        receiverReliability.Dispose();
    }
}
