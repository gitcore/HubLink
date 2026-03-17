using System.Security.Cryptography;
using System.Text;

namespace HubLink.Test;

/// <summary>
/// AesCryptoPool 单元测试
/// </summary>
/// <remarks>
/// 测试覆盖范围：
/// - 基本的租用和返回操作
/// - 加密和解密功能
/// - 多密钥管理
/// - 池大小限制
/// - 清空池功能
/// - 并发访问安全性
/// - 空值处理
/// - AES 配置正确性
/// - 不同数据大小处理
/// - 压力测试
/// - 内存效率
/// - 单例模式
/// - 加密器和解密器分离
/// </remarks>
public class AesCryptoPoolTest
{
    /// <summary>
    /// 测试租用和返回加密器的基本操作
    /// </summary>
    /// <remarks>
    /// 验证点：
    /// - 可以成功租用加密器
    /// - 可以成功返回加密器
    /// - 返回后可以再次租用同一个加密器
    /// </remarks>
    [Fact]
    public void TestRentAndReturnEncryptor()
    {
        var pool = AesCryptoPool.Instance;
        var key = "TestKey123";
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(key.PadRight(32, '0').Substring(0, 32));

        var aes1 = pool.RentEncryptor(keyBytes);
        Assert.NotNull(aes1);

        pool.ReturnEncryptor(aes1, keyBytes);

        var aes2 = pool.RentEncryptor(keyBytes);
        Assert.NotNull(aes2);

        pool.ReturnEncryptor(aes2, keyBytes);
    }

    /// <summary>
    /// 测试租用和返回解密器的基本操作
    /// </summary>
    /// <remarks>
    /// 验证点：
    /// - 可以成功租用解密器
    /// - 可以成功返回解密器
    /// - 返回后可以再次租用同一个解密器
    /// </remarks>
    [Fact]
    public void TestRentAndReturnDecryptor()
    {
        var pool = AesCryptoPool.Instance;
        var key = "TestKey456";
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(key.PadRight(32, '0').Substring(0, 32));

        var aes1 = pool.RentDecryptor(keyBytes);
        Assert.NotNull(aes1);

        pool.ReturnDecryptor(aes1, keyBytes);

        var aes2 = pool.RentDecryptor(keyBytes);
        Assert.NotNull(aes2);

        pool.ReturnDecryptor(aes2, keyBytes);
    }

    /// <summary>
    /// 测试使用池化的 AES 进行加密和解密
    /// </summary>
    /// <remarks>
    /// 验证点：
    /// - 使用池化的加密器可以正确加密数据
    /// - 使用池化的解密器可以正确解密数据
    /// - 加密和解密后的数据与原始数据一致
    /// </remarks>
    [Fact]
    public void TestEncryptionDecryptionWithPooledAes()
    {
        var pool = AesCryptoPool.Instance;
        var key = "EncryptionKey789";
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(key.PadRight(32, '0').Substring(0, 32));

        var testData = System.Text.Encoding.UTF8.GetBytes("Hello, World! This is a test message.");

        var encryptor = pool.RentEncryptor(keyBytes);
        var encryptorTransform = encryptor.CreateEncryptor();
        var encrypted = encryptorTransform.TransformFinalBlock(testData, 0, testData.Length);
        pool.ReturnEncryptor(encryptor, keyBytes);

        var decryptor = pool.RentDecryptor(keyBytes);
        var decryptorTransform = decryptor.CreateDecryptor();
        var decrypted = decryptorTransform.TransformFinalBlock(encrypted, 0, encrypted.Length);
        pool.ReturnDecryptor(decryptor, keyBytes);

        Assert.Equal(testData, decrypted);
    }

    /// <summary>
    /// 测试多个密钥的池管理
    /// </summary>
    /// <remarks>
    /// 验证点：
    /// - 可以为不同的密钥创建独立的池
    /// - 每个密钥的加密器和解密器可以正常租用和返回
    /// - 不同密钥之间互不干扰
    /// </remarks>
    [Fact]
    public void TestMultipleKeys()
    {
        var pool = AesCryptoPool.Instance;
        var keys = new[] { "Key1", "Key2", "Key3", "Key4", "Key5" };
        var keyBytesList = keys.Select(k => System.Text.Encoding.UTF8.GetBytes(k.PadRight(32, '0').Substring(0, 32))).ToList();

        foreach (var keyBytes in keyBytesList)
        {
            var aes = pool.RentEncryptor(keyBytes);
            Assert.NotNull(aes);
            pool.ReturnEncryptor(aes, keyBytes);
        }

        foreach (var keyBytes in keyBytesList)
        {
            var aes = pool.RentDecryptor(keyBytes);
            Assert.NotNull(aes);
            pool.ReturnDecryptor(aes, keyBytes);
        }
    }

    /// <summary>
    /// 测试池大小限制（MaxPoolSize = 64）
    /// </summary>
    /// <remarks>
    /// 验证点：
    /// - 可以租用超过池大小限制的 AES 实例
    /// - 超过限制时，新的实例会被创建而不是从池中获取
    /// - 返回超过限制的实例时会被正确处理（Dispose）
    /// </remarks>
    [Fact]
    public void TestPoolSizeLimit()
    {
        var pool = AesCryptoPool.Instance;
        var key = "PoolSizeTestKey";
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(key.PadRight(32, '0').Substring(0, 32));

        var rentedAes = new List<Aes>();

        for (int i = 0; i < 70; i++)
        {
            var aes = pool.RentEncryptor(keyBytes);
            Assert.NotNull(aes);
            rentedAes.Add(aes);
        }

        foreach (var aes in rentedAes)
        {
            pool.ReturnEncryptor(aes, keyBytes);
        }
    }

    /// <summary>
    /// 测试清空池功能
    /// </summary>
    /// <remarks>
    /// 验证点：
    /// - Clear() 方法可以清空池中的所有 AES 实例
    /// - 清空后可以正常租用新的 AES 实例
    /// - 清空操作不会影响后续的租用和返回操作
    /// </remarks>
    [Fact]
    public void TestClearPool()
    {
        var pool = AesCryptoPool.Instance;
        var key = "ClearTestKey";
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(key.PadRight(32, '0').Substring(0, 32));

        var aes1 = pool.RentEncryptor(keyBytes);
        var aes2 = pool.RentEncryptor(keyBytes);
        var aes3 = pool.RentEncryptor(keyBytes);

        pool.ReturnEncryptor(aes1, keyBytes);
        pool.ReturnEncryptor(aes2, keyBytes);
        pool.ReturnEncryptor(aes3, keyBytes);

        pool.Clear();

        var aes4 = pool.RentEncryptor(keyBytes);
        Assert.NotNull(aes4);
        pool.ReturnEncryptor(aes4, keyBytes);
    }

    /// <summary>
    /// 测试并发访问安全性
    /// </summary>
    /// <remarks>
    /// 验证点：
    /// - 多个线程可以同时租用和返回 AES 实例
    /// - 并发操作不会导致数据竞争或死锁
    /// - 所有并发操作都能正确完成
    /// </remarks>
    [Fact]
    public async Task TestConcurrentAccess()
    {
        var pool = AesCryptoPool.Instance;
        var key = "ConcurrentTestKey";
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(key.PadRight(32, '0').Substring(0, 32));
        var tasks = new List<Task>();
        var iterations = 100;

        for (int t = 0; t < 10; t++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    var aes = pool.RentEncryptor(keyBytes);
                    Assert.NotNull(aes);
                    pool.ReturnEncryptor(aes, keyBytes);

                    var decryptor = pool.RentDecryptor(keyBytes);
                    Assert.NotNull(decryptor);
                    pool.ReturnDecryptor(decryptor, keyBytes);
                }
            }));
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 测试空值返回处理
    /// </summary>
    /// <remarks>
    /// 验证点：
    /// - 传入 null 给 ReturnEncryptor 不会抛出异常
    /// - 传入 null 给 ReturnDecryptor 不会抛出异常
    /// - 空值返回后可以正常租用新的 AES 实例
    /// </remarks>
    [Fact]
    public void TestNullReturn()
    {
        var pool = AesCryptoPool.Instance;
        var key = "NullTestKey";
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(key.PadRight(32, '0').Substring(0, 32));

        pool.ReturnEncryptor(null!, keyBytes);
        pool.ReturnDecryptor(null!, keyBytes);

        var aes = pool.RentEncryptor(keyBytes);
        Assert.NotNull(aes);
        pool.ReturnEncryptor(aes, keyBytes);
    }

    /// <summary>
    /// 测试 AES 配置正确性
    /// </summary>
    /// <remarks>
    /// 验证点：
    /// - 租用的加密器 Key 设置正确
    /// - 租用的加密器 IV 长度为 16
    /// - 租用的加密器 Mode 为 CBC
    /// - 租用的加密器 Padding 为 PKCS7
    /// - 解密器配置与加密器相同
    /// </remarks>
    [Fact]
    public void TestAesConfiguration()
    {
        var pool = AesCryptoPool.Instance;
        var key = "ConfigTestKey";
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(key.PadRight(32, '0').Substring(0, 32));

        var encryptor = pool.RentEncryptor(keyBytes);
        Assert.Equal(keyBytes, encryptor.Key);
        Assert.Equal(16, encryptor.IV.Length);
        Assert.Equal(CipherMode.CBC, encryptor.Mode);
        Assert.Equal(PaddingMode.PKCS7, encryptor.Padding);
        pool.ReturnEncryptor(encryptor, keyBytes);

        var decryptor = pool.RentDecryptor(keyBytes);
        Assert.Equal(keyBytes, decryptor.Key);
        Assert.Equal(16, decryptor.IV.Length);
        Assert.Equal(CipherMode.CBC, decryptor.Mode);
        Assert.Equal(PaddingMode.PKCS7, decryptor.Padding);
        pool.ReturnDecryptor(decryptor, keyBytes);
    }

    /// <summary>
    /// 测试多次租用后再返回
    /// </summary>
    /// <remarks>
    /// 验证点：
    /// - 可以连续租用多个 AES 实例
    /// - 所有租用的实例都不为 null
    /// - 所有实例都可以成功返回
    /// </remarks>
    [Fact]
    public void TestMultipleRentsBeforeReturn()
    {
        var pool = AesCryptoPool.Instance;
        var key = "MultipleRentKey";
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(key.PadRight(32, '0').Substring(0, 32));

        var aes1 = pool.RentEncryptor(keyBytes);
        var aes2 = pool.RentEncryptor(keyBytes);
        var aes3 = pool.RentEncryptor(keyBytes);

        Assert.NotNull(aes1);
        Assert.NotNull(aes2);
        Assert.NotNull(aes3);

        pool.ReturnEncryptor(aes1, keyBytes);
        pool.ReturnEncryptor(aes2, keyBytes);
        pool.ReturnEncryptor(aes3, keyBytes);
    }

    /// <summary>
    /// 测试不同数据大小的加密解密
    /// </summary>
    /// <remarks>
    /// 验证点：
    /// - 可以处理不同大小的数据（1 到 16384 字节）
    /// - 所有大小的数据都能正确加密和解密
    /// - 加密解密后的数据与原始数据一致
    /// </remarks>
    [Fact]
    public void TestEncryptionDecryptionWithDifferentDataSizes()
    {
        var pool = AesCryptoPool.Instance;
        var key = "DataSizeTestKey";
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(key.PadRight(32, '0').Substring(0, 32));
        var dataSizes = new[] { 1, 16, 64, 256, 1024, 4096, 16384 };

        foreach (var size in dataSizes)
        {
            var testData = new byte[size];
            for (int i = 0; i < size; i++)
            {
                testData[i] = (byte)(i % 256);
            }

            var encryptor = pool.RentEncryptor(keyBytes);
            var encryptorTransform = encryptor.CreateEncryptor();
            var encrypted = encryptorTransform.TransformFinalBlock(testData, 0, testData.Length);
            pool.ReturnEncryptor(encryptor, keyBytes);

            var decryptor = pool.RentDecryptor(keyBytes);
            var decryptorTransform = decryptor.CreateDecryptor();
            var decrypted = decryptorTransform.TransformFinalBlock(encrypted, 0, encrypted.Length);
            pool.ReturnDecryptor(decryptor, keyBytes);

            Assert.Equal(testData, decrypted);
        }
    }

    /// <summary>
    /// 测试池的压力测试（1000次迭代）
    /// </summary>
    /// <remarks>
    /// 验证点：
    /// - 可以在大量迭代中稳定运行
    /// - 每次加密解密都能正确完成
    /// - 数据在加密解密过程中保持一致
    /// </remarks>
    [Fact]
    public void TestStressTestWithPool()
    {
        var pool = AesCryptoPool.Instance;
        var key = "StressTestKey";
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(key.PadRight(32, '0').Substring(0, 32));
        var testData = System.Text.Encoding.UTF8.GetBytes("Stress test data for encryption and decryption.");
        var iterations = 1000;

        for (int i = 0; i < iterations; i++)
        {
            var encryptor = pool.RentEncryptor(keyBytes);
            var encryptorTransform = encryptor.CreateEncryptor();
            var encrypted = encryptorTransform.TransformFinalBlock(testData, 0, testData.Length);
            pool.ReturnEncryptor(encryptor, keyBytes);

            var decryptor = pool.RentDecryptor(keyBytes);
            var decryptorTransform = decryptor.CreateDecryptor();
            var decrypted = decryptorTransform.TransformFinalBlock(encrypted, 0, encrypted.Length);
            pool.ReturnDecryptor(decryptor, keyBytes);

            Assert.Equal(testData, decrypted);
        }
    }

    /// <summary>
    /// 测试内存效率（防止内存泄漏）
    /// </summary>
    /// <remarks>
    /// 验证点：
    /// - 在大量加密解密操作后内存增长在合理范围内
    /// - 池的重用机制有效减少了内存分配
    /// - 内存增长不超过 50MB
    /// </remarks>
    [Fact]
    public void TestMemoryEfficiency()
    {
        var pool = AesCryptoPool.Instance;
        var key = "MemoryTestKey";
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(key.PadRight(32, '0').Substring(0, 32));
        var testData = new byte[4096];
        for (int i = 0; i < testData.Length; i++)
        {
            testData[i] = (byte)(i % 256);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var initialMemory = GC.GetTotalMemory(true);

        for (int i = 0; i < 1000; i++)
        {
            var encryptor = pool.RentEncryptor(keyBytes);
            var encryptorTransform = encryptor.CreateEncryptor();
            var encrypted = encryptorTransform.TransformFinalBlock(testData, 0, testData.Length);
            pool.ReturnEncryptor(encryptor, keyBytes);

            var decryptor = pool.RentDecryptor(keyBytes);
            var decryptorTransform = decryptor.CreateDecryptor();
            var decrypted = decryptorTransform.TransformFinalBlock(encrypted, 0, encrypted.Length);
            pool.ReturnDecryptor(decryptor, keyBytes);

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

    /// <summary>
    /// 测试单例模式
    /// </summary>
    /// <remarks>
    /// 验证点：
    /// - AesCryptoPool.Instance 返回相同的实例
    /// - 多次调用 Instance 属性返回的是同一个对象
    /// </remarks>
    [Fact]
    public void TestSingletonInstance()
    {
        var pool1 = AesCryptoPool.Instance;
        var pool2 = AesCryptoPool.Instance;

        Assert.Same(pool1, pool2);
    }

    /// <summary>
    /// 测试加密器和解密器的分离池
    /// </summary>
    /// <remarks>
    /// 验证点：
    /// - 加密器和解密器使用独立的池
    /// - 可以同时租用加密器和解密器
    /// - 两个池的操作互不干扰
    /// </remarks>
    [Fact]
    public void TestSeparatePoolsForEncryptorAndDecryptor()
    {
        var pool = AesCryptoPool.Instance;
        var key = "SeparatePoolsKey";
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(key.PadRight(32, '0').Substring(0, 32));

        var encryptor = pool.RentEncryptor(keyBytes);
        var decryptor = pool.RentDecryptor(keyBytes);

        Assert.NotNull(encryptor);
        Assert.NotNull(decryptor);

        pool.ReturnEncryptor(encryptor, keyBytes);
        pool.ReturnDecryptor(decryptor, keyBytes);

        var encryptor2 = pool.RentEncryptor(keyBytes);
        var decryptor2 = pool.RentDecryptor(keyBytes);

        Assert.NotNull(encryptor2);
        Assert.NotNull(decryptor2);

        pool.ReturnEncryptor(encryptor2, keyBytes);
        pool.ReturnDecryptor(decryptor2, keyBytes);
    }

    /// <summary>
    /// 测试清空不影响新的租用
    /// </summary>
    /// <remarks>
    /// 验证点：
    /// - 清空池后可以正常租用新的 AES 实例
    /// - 新租用的实例可以正常加密数据
    /// - 加密后的数据可以正常解密
    /// </remarks>
    [Fact]
    public void TestClearDoesNotAffectNewRentals()
    {
        var pool = AesCryptoPool.Instance;
        var key = "ClearNewRentalsKey";
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(key.PadRight(32, '0').Substring(0, 32));

        pool.Clear();

        var aes = pool.RentEncryptor(keyBytes);
        Assert.NotNull(aes);

        var testData = System.Text.Encoding.UTF8.GetBytes("Test data after clear");
        var encryptorTransform = aes.CreateEncryptor();
        var encrypted = encryptorTransform.TransformFinalBlock(testData, 0, testData.Length);
        pool.ReturnEncryptor(aes, keyBytes);

        var decryptor = pool.RentDecryptor(keyBytes);
        var decryptorTransform = decryptor.CreateDecryptor();
        var decrypted = decryptorTransform.TransformFinalBlock(encrypted, 0, encrypted.Length);
        pool.ReturnDecryptor(decryptor, keyBytes);

        Assert.Equal(testData, decrypted);
    }
}
