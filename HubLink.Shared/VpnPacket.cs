namespace HubLink.Shared;

public class VpnPacket
{
    public string ClientId { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 0;
    public byte[] Payload { get; set; } = [];
}