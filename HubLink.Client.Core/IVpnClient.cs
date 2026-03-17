namespace HubLink.Client;

public interface IVpnClient
{
    Task<int> ConnectAsync(string serverName);
    Task<int> DisconnectAsync();
    Task<(int resultCode, VpnStatusResponse status)> GetStatusAsync();
    Task<(int resultCode, ServerListResponse servers)> GetServersAsync();
    Task<(int resultCode, TrafficInfo traffic)> GetTrafficAsync();
    Task<int> EnableProxyAsync();
    Task<int> DisableProxyAsync();
}
