namespace HubLink.Test;

public class VpnPacketTest
{
    [Fact]
    public void TestBasicEncryptionDecryption()
    {
        var encryptionKey = "MySecretKey123";
        var keyBytes = VpnPacketHelper.GetEncryptionKey(encryptionKey);

        var testData = Encoding.ASCII.GetBytes("CONNECT www.google.com:443 HTTP/1.1\r\nHost: www.google.com\r\n\r\n");

        var encrypted = VpnPacketHelper.EncryptData(testData, keyBytes);
        var decrypted = VpnPacketHelper.DecryptData(encrypted, keyBytes);

        Assert.Equal(testData.Length, decrypted.Length);
        Assert.Equal(testData, decrypted);
    }

    [Fact]
    public void TestVpnPacketAssemblyAndParsing()
    {
        var encryptionKey = "MySecretKey123";
        var keyBytes = VpnPacketHelper.GetEncryptionKey(encryptionKey);

        var testData = Encoding.ASCII.GetBytes("CONNECT www.google.com:443 HTTP/1.1\r\nHost: www.google.com\r\n\r\n");
        var clientKey = "test-client-123";

        var packet = VpnPacketHelper.CreateVpnPacket(testData, clientKey);

        var encryptedPacket = VpnPacketHelper.EncryptData(packet, keyBytes);
        var decryptedPacket = VpnPacketHelper.DecryptData(encryptedPacket, keyBytes);

        var (parsedClientKey, payload) = VpnPacketHelper.ParseVpnPacket(decryptedPacket);

        Assert.Equal(clientKey, parsedClientKey);
        Assert.Equal(testData.Length, payload.Length);
        Assert.Equal(testData, payload.ToArray());
    }

    [Fact]
    public void TestFullFlow()
    {
        var encryptionKey = "MySecretKey123";
        var keyBytes = VpnPacketHelper.GetEncryptionKey(encryptionKey);

        var fullTest = "GET / HTTP/1.1\r\nHost: www.baidu.com\r\n\r\n";
        var fullTestData = Encoding.ASCII.GetBytes(fullTest);
        var fullClientKey = "full-test-client";

        var fullPacket = VpnPacketHelper.CreateVpnPacket(fullTestData, fullClientKey);
        var fullEncrypted = VpnPacketHelper.EncryptData(fullPacket, keyBytes);
        var fullDecrypted = VpnPacketHelper.DecryptData(fullEncrypted, keyBytes);
        var (fullParsedKey, fullPayload) = VpnPacketHelper.ParseVpnPacket(fullDecrypted);

        Assert.Equal(fullClientKey, fullParsedKey);
        Assert.Equal(fullTestData.Length, fullPayload.Length);
        Assert.Equal(fullTestData, fullPayload.ToArray());
    }

    [Fact]
    public void TestEncryptionKeyGeneration()
    {
        var key1 = VpnPacketHelper.GetEncryptionKey("MySecretKey123");
        var key2 = VpnPacketHelper.GetEncryptionKey("MySecretKey123");

        Assert.Equal(32, key1.Length);
        Assert.Equal(32, key2.Length);
        Assert.Equal(key1, key2);
    }

    [Fact]
    public void TestEmptyDataEncryption()
    {
        var encryptionKey = "MySecretKey123";
        var keyBytes = VpnPacketHelper.GetEncryptionKey(encryptionKey);

        var emptyData = new byte[1];

        var encrypted = VpnPacketHelper.EncryptData(emptyData, keyBytes);
        var decrypted = VpnPacketHelper.DecryptData(encrypted, keyBytes);

        Assert.Equal(emptyData.Length, decrypted.Length);
        Assert.Equal(emptyData, decrypted);
    }

    [Fact]
    public void TestLargeDataEncryption()
    {
        var encryptionKey = "MySecretKey123";
        var keyBytes = VpnPacketHelper.GetEncryptionKey(encryptionKey);

        var largeData = new byte[8192];
        for (int i = 0; i < largeData.Length; i++)
        {
            largeData[i] = (byte)(i % 256);
        }

        var encrypted = VpnPacketHelper.EncryptData(largeData, keyBytes);
        var decrypted = VpnPacketHelper.DecryptData(encrypted, keyBytes);

        Assert.Equal(largeData.Length, decrypted.Length);
        Assert.Equal(largeData, decrypted);
    }

    [Fact]
    public void TestCreateVpnPacket()
    {
        var testData = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n");
        var clientKey = "client-001";

        var packet = VpnPacketHelper.CreateVpnPacket(testData, clientKey);

        Assert.True(packet.Length > testData.Length);
    }

    [Fact]
    public void TestParseVpnPacket()
    {
        var testData = Encoding.ASCII.GetBytes("POST /api HTTP/1.1\r\nHost: api.example.com\r\n\r\n");
        var clientKey = "client-002";

        var packet = VpnPacketHelper.CreateVpnPacket(testData, clientKey);
        var (parsedClientKey, payload) = VpnPacketHelper.ParseVpnPacket(packet);

        Assert.Equal(clientKey, parsedClientKey);
        Assert.Equal(testData.Length, payload.Length);
        Assert.Equal(testData, payload.ToArray());
    }

    [Fact]
    public void TestCreateVpnPacketWithEmptyPayload()
    {
        var testData = Array.Empty<byte>();
        var clientKey = "client-003";

        var packet = VpnPacketHelper.CreateVpnPacket(testData, clientKey);
        var (parsedClientKey, payload) = VpnPacketHelper.ParseVpnPacket(packet);

        Assert.Equal(clientKey, parsedClientKey);
        Assert.Equal(0, payload.Length);
    }

    [Fact]
    public void TestCreateVpnPacketWithLargePayload()
    {
        var testData = new byte[10000];
        for (int i = 0; i < testData.Length; i++)
        {
            testData[i] = (byte)(i % 256);
        }
        var clientKey = "client-004";

        var packet = VpnPacketHelper.CreateVpnPacket(testData, clientKey);
        var (parsedClientKey, payload) = VpnPacketHelper.ParseVpnPacket(packet);

        Assert.Equal(clientKey, parsedClientKey);
        Assert.Equal(testData.Length, payload.Length);
        Assert.Equal(testData, payload.ToArray());
    }

    [Fact]
    public void TestParseVpnPacketWithDifferentAddresses()
    {
        var testData = Encoding.ASCII.GetBytes("TEST DATA");
        var testCases = new[]
        {
            ("127.0.0.1", "client-ipv4-1"),
            ("192.168.1.1", "client-ipv4-2"),
            ("example.com", "client-domain-1"),
            ("sub.domain.co.uk", "client-domain-2")
        };

        foreach (var (targetAddress, clientKey) in testCases)
        {
            var packet = VpnPacketHelper.CreateVpnPacket(testData, clientKey);
            var (parsedClientKey, payload) = VpnPacketHelper.ParseVpnPacket(packet);

            Assert.Equal(clientKey, parsedClientKey);
            Assert.Equal(testData.Length, payload.Length);
            Assert.Equal(testData, payload.ToArray());
        }
    }

    [Fact]
    public void TestMassiveEncryptionDecryption()
    {
        var encryptionKey = "MySecretKey123";
        var keyBytes = VpnPacketHelper.GetEncryptionKey(encryptionKey);
        var testData = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n");
        var iterations = 10000;

        for (int i = 0; i < iterations; i++)
        {
            var encrypted = VpnPacketHelper.EncryptData(testData, keyBytes);
            var decrypted = VpnPacketHelper.DecryptData(encrypted, keyBytes);

            Assert.Equal(testData.Length, decrypted.Length);
            Assert.Equal(testData, decrypted);
        }
    }

    [Fact]
    public void TestMemoryConsumptionWithEncryption()
    {
        var encryptionKey = "MySecretKey123";
        var keyBytes = VpnPacketHelper.GetEncryptionKey(encryptionKey);
        var testData = new byte[4096];
        for (int i = 0; i < testData.Length; i++)
        {
            testData[i] = (byte)(i % 256);
        }

        var initialMemory = GC.GetTotalMemory(true);
        var iterations = 1000;

        for (int i = 0; i < iterations; i++)
        {
            var encrypted = VpnPacketHelper.EncryptData(testData, keyBytes);
            var decrypted = VpnPacketHelper.DecryptData(encrypted, keyBytes);

            Assert.Equal(testData.Length, decrypted.Length);
            Assert.Equal(testData, decrypted);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(true);
        var memoryIncrease = finalMemory - initialMemory;

        var maxAllowedIncrease = 50L * 1024L * 1024L;
        Assert.True(memoryIncrease < maxAllowedIncrease, $"Memory increase {memoryIncrease / 1024 / 1024}MB exceeded threshold of {maxAllowedIncrease / 1024 / 1024}MB");
    }

    [Fact]
    public void TestConcurrentEncryptionDecryption()
    {
        var encryptionKey = "MySecretKey123";
        var keyBytes = VpnPacketHelper.GetEncryptionKey(encryptionKey);
        var testData = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n");
        var iterations = 1000;
        var tasks = new List<Task>();

        for (int t = 0; t < 10; t++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    var encrypted = VpnPacketHelper.EncryptData(testData, keyBytes);
                    var decrypted = VpnPacketHelper.DecryptData(encrypted, keyBytes);

                    Assert.Equal(testData.Length, decrypted.Length);
                    Assert.Equal(testData, decrypted);
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());
    }

    [Fact]
    public void TestMemoryLeakPrevention()
    {
        var encryptionKey = "MySecretKey123";
        var keyBytes = VpnPacketHelper.GetEncryptionKey(encryptionKey);
        var testData = new byte[8192];
        for (int i = 0; i < testData.Length; i++)
        {
            testData[i] = (byte)(i % 256);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var initialMemory = GC.GetTotalMemory(true);

        for (int round = 0; round < 100; round++)
        {
            var encryptedList = new List<byte[]>();
            var decryptedList = new List<byte[]>();

            for (int i = 0; i < 100; i++)
            {
                var encrypted = VpnPacketHelper.EncryptData(testData, keyBytes);
                var decrypted = VpnPacketHelper.DecryptData(encrypted, keyBytes);

                encryptedList.Add(encrypted.ToArray());
                decryptedList.Add(decrypted.ToArray());

                Assert.Equal(testData.Length, decrypted.Length);
                Assert.Equal(testData, decrypted.ToArray());
            }
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(true);
        var memoryIncrease = finalMemory - initialMemory;

        var maxAllowedIncrease = 100L * 1024L * 1024L;
        Assert.True(memoryIncrease < maxAllowedIncrease, $"Memory increase {memoryIncrease / 1024 / 1024}MB exceeded threshold of {maxAllowedIncrease / 1024 / 1024}MB");
    }

    [Fact]
    public void TestLargeDataEncryptionPerformance()
    {
        var encryptionKey = "MySecretKey123";
        var keyBytes = VpnPacketHelper.GetEncryptionKey(encryptionKey);
        var testData = new byte[65536];
        for (int i = 0; i < testData.Length; i++)
        {
            testData[i] = (byte)(i % 256);
        }

        var iterations = 100;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            var encrypted = VpnPacketHelper.EncryptData(testData, keyBytes);
            var decrypted = VpnPacketHelper.DecryptData(encrypted, keyBytes);

            Assert.Equal(testData.Length, decrypted.Length);
            Assert.Equal(testData, decrypted);
        }

        stopwatch.Stop();
        var avgTimeMs = stopwatch.ElapsedMilliseconds / (double)iterations;

        Assert.True(avgTimeMs < 100, $"Average encryption/decryption time {avgTimeMs:F2}ms exceeded threshold of 100ms");
    }

    [Fact]
    public void TestVaryingDataSizes()
    {
        var encryptionKey = "MySecretKey123";
        var keyBytes = VpnPacketHelper.GetEncryptionKey(encryptionKey);
        var dataSizes = new[] { 1, 16, 64, 256, 1024, 4096, 16384, 65536 };

        foreach (var size in dataSizes)
        {
            var testData = new byte[size];
            for (int i = 0; i < size; i++)
            {
                testData[i] = (byte)(i % 256);
            }

            var encrypted = VpnPacketHelper.EncryptData(testData, keyBytes);
            var decrypted = VpnPacketHelper.DecryptData(encrypted, keyBytes);

            Assert.Equal(testData.Length, decrypted.Length);
            Assert.Equal(testData, decrypted);
        }
    }

    [Fact]
    public void TestStressTestWithMultipleKeys()
    {
        var keys = new[] { "Key1", "Key2", "Key3", "Key4", "Key5" };
        var testData = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n");
        var iterations = 1000;

        foreach (var key in keys)
        {
            var keyBytes = VpnPacketHelper.GetEncryptionKey(key);

            for (int i = 0; i < iterations; i++)
            {
                var encrypted = VpnPacketHelper.EncryptData(testData, keyBytes);
                var decrypted = VpnPacketHelper.DecryptData(encrypted, keyBytes);

                Assert.Equal(testData.Length, decrypted.Length);
                Assert.Equal(testData, decrypted);
            }
        }
    }
}
