using System.Text.Json.Serialization;
using HubLink.Client.Models;

namespace HubLink.Client
{
    [JsonSerializable(typeof(VpnConfig))]
    [JsonSerializable(typeof(VpnServerConfig))]
    [JsonSerializable(typeof(List<VpnServerConfig>))]
    [JsonSerializable(typeof(ApiResponse<object>))]
    [JsonSerializable(typeof(ApiResponse<VpnStatusResponse>))]
    [JsonSerializable(typeof(ApiResponse<TrafficInfo>))]
    [JsonSerializable(typeof(ApiResponse<ProxyStatusResponse>))]
    [JsonSerializable(typeof(ApiResponse<ServerListResponse>))]
    [JsonSerializable(typeof(ApiResponse<ConfigInfo>))]
    [JsonSerializable(typeof(ApiResponse<LogsResponse>))]
    [JsonSerializable(typeof(VpnStatusResponse))]
    [JsonSerializable(typeof(TrafficInfo))]
    [JsonSerializable(typeof(VpnServerInfo))]
    [JsonSerializable(typeof(ServerListResponse))]
    [JsonSerializable(typeof(AddServerRequest))]
    [JsonSerializable(typeof(ProxyStatusResponse))]
    [JsonSerializable(typeof(LogEntry))]
    [JsonSerializable(typeof(LogsResponse))]
    [JsonSerializable(typeof(ConfigResponse))]
    [JsonSerializable(typeof(UpdateConfigRequest))]
    [JsonSerializable(typeof(ConfigInfo))]
    [JsonSerializable(typeof(object))]
    [JsonSerializable(typeof(TunnelInfo))]
    [JsonSerializable(typeof(byte[]))]
    [JsonSerializable(typeof(ReadOnlyMemory<byte>))]
    public partial class AppJsonContext : JsonSerializerContext
    {
    }
}