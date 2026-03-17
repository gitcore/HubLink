namespace HubLink.Shared;

public class AesCryptoPool
{
    private static readonly AesCryptoPool _instance = new AesCryptoPool();
    public static AesCryptoPool Instance => _instance;

    private readonly Dictionary<string, Stack<Aes>> _encryptorPool = new();
    private readonly Dictionary<string, Stack<Aes>> _decryptorPool = new();
    private readonly object _lock = new();
    private const int MaxPoolSize = 64;

    private AesCryptoPool()
    {
    }

    public Aes RentEncryptor(byte[] key)
    {
        var keyString = Convert.ToBase64String(key);
        
        lock (_lock)
        {
            if (_encryptorPool.TryGetValue(keyString, out var pool) && pool.Count > 0)
            {
                var aes = pool.Pop();
                aes.Clear();
                aes.Key = key;
                aes.IV = new byte[16];
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                return aes;
            }
        }

        var newAes = Aes.Create();
        newAes.Key = key;
        newAes.IV = new byte[16];
        newAes.Mode = CipherMode.CBC;
        newAes.Padding = PaddingMode.PKCS7;
        return newAes;
    }

    public Aes RentDecryptor(byte[] key)
    {
        var keyString = Convert.ToBase64String(key);
        
        lock (_lock)
        {
            if (_decryptorPool.TryGetValue(keyString, out var pool) && pool.Count > 0)
            {
                var aes = pool.Pop();
                aes.Clear();
                aes.Key = key;
                aes.IV = new byte[16];
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                return aes;
            }
        }

        var newAes = Aes.Create();
        newAes.Key = key;
        newAes.IV = new byte[16];
        newAes.Mode = CipherMode.CBC;
        newAes.Padding = PaddingMode.PKCS7;
        return newAes;
    }

    public void ReturnEncryptor(Aes aes, byte[] key)
    {
        if (aes == null) return;
        
        var keyString = Convert.ToBase64String(key);
        
        lock (_lock)
        {
            if (!_encryptorPool.ContainsKey(keyString))
            {
                _encryptorPool[keyString] = new Stack<Aes>();
            }

            var pool = _encryptorPool[keyString];
            if (pool.Count < MaxPoolSize)
            {
                aes.Clear();
                pool.Push(aes);
            }
            else
            {
                aes.Dispose();
            }
        }
    }

    public void ReturnDecryptor(Aes aes, byte[] key)
    {
        if (aes == null) return;
        
        var keyString = Convert.ToBase64String(key);
        
        lock (_lock)
        {
            if (!_decryptorPool.ContainsKey(keyString))
            {
                _decryptorPool[keyString] = new Stack<Aes>();
            }

            var pool = _decryptorPool[keyString];
            if (pool.Count < MaxPoolSize)
            {
                aes.Clear();
                pool.Push(aes);
            }
            else
            {
                aes.Dispose();
            }
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            foreach (var pool in _encryptorPool.Values)
            {
                foreach (var aes in pool)
                {
                    aes.Dispose();
                }
            }
            _encryptorPool.Clear();

            foreach (var pool in _decryptorPool.Values)
            {
                foreach (var aes in pool)
                {
                    aes.Dispose();
                }
            }
            _decryptorPool.Clear();
        }
    }
}
