using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging;

namespace HubLink.Server.Services;

public class DnsResolver
{
    private readonly ConcurrentDictionary<string, DnsCacheEntry> _dnsCache = new();
    private readonly ILogger<DnsResolver> _logger;
    private readonly TimeSpan _dnsCacheTime = TimeSpan.FromMinutes(15);

    public DnsResolver(ILogger<DnsResolver> logger)
    {
        _logger = logger;
    }

    public async Task<IPAddress?> ResolveAddressAsync(string hostname)
    {
        try
        {
            if (IPAddress.TryParse(hostname, out var ipAddress))
            {
                return ipAddress;
            }

            if (_dnsCache.TryGetValue(hostname, out var cachedEntry))
            {
                var cacheAge = DateTime.UtcNow - cachedEntry.CacheTime;
                if (cacheAge < _dnsCacheTime)
                {
                    _logger.LogDebug("DNS cache hit for {Hostname}, age: {Age} seconds", hostname, cacheAge.TotalSeconds);
                    return cachedEntry.Address;
                }
            }

            var addressList = await Dns.GetHostAddressesAsync(hostname);
            ipAddress = addressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
            if (ipAddress != null)
            {
                _dnsCache.TryAdd(hostname, new DnsCacheEntry { Address = ipAddress, CacheTime = DateTime.UtcNow });
                _logger.LogDebug("DNS resolved {Hostname} to {IpAddress} (IPv4)", hostname, ipAddress);
                return ipAddress;
            }

            ipAddress = addressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetworkV6);
            if (ipAddress != null)
            {
                _dnsCache.TryAdd(hostname, new DnsCacheEntry { Address = ipAddress, CacheTime = DateTime.UtcNow });
                _logger.LogDebug("DNS resolved {Hostname} to {IpAddress} (IPv6)", hostname, ipAddress);
                return ipAddress;
            }

            _logger.LogWarning("No valid IP address found for {Hostname}", hostname);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve {Hostname}", hostname);
            return null;
        }
    }

    public void ClearCache()
    {
        _dnsCache.Clear();
        _logger.LogInformation("DNS cache cleared");
    }
}

public class DnsCacheEntry
{
    public required IPAddress Address { get; set; }
    public DateTime CacheTime { get; set; }
}
