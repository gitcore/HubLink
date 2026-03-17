namespace HubLink.Shared;

public class TunnelInfo
{
    public string ClientId { get; set; } = string.Empty;
    public bool EnableEncryption { get; set; } = false;
    public byte[] EncryptionKey { get; set; } = [];
    public int IdleTimeoutSeconds { get; set; } = 180;
    public string Version { get; set; } = "1.0";
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public bool EnableReliablePackets { get; set; } = true;
}