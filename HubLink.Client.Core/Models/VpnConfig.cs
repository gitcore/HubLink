namespace HubLink.Client.Models
{
    public class VpnServerConfig
    {
        public string Name { get; set; } = "default";
        public string ServerUrl { get; set; } = "http://localhost:4080";
        public int LocalPort { get; set; } = 1080;
        public string EncryptionKey { get; set; } = string.Empty;
        public bool EnableEncryption { get; set; } = true;
        public bool AutoReconnect { get; set; } = true;
        public int ReconnectInterval { get; set; } = 5000;
        public bool AutoProxy { get; set; } = true;

        public override string ToString()
        {
            return $"{Name} ({ServerUrl})";
        }
    }

    public class VpnConfig
    {
        public List<VpnServerConfig> Servers { get; set; } = new();
        public string? LastUsedServer { get; set; }

        public void Save(string filePath)
        {
            var json = JsonSerializer.Serialize(this, AppJsonContext.Default.VpnConfig);
            File.WriteAllText(filePath, json);
        }

        public static VpnConfig Load(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return new VpnConfig();
            }

            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize(json, AppJsonContext.Default.VpnConfig) ?? new VpnConfig();
        }

        public VpnServerConfig? GetLastUsedServer()
        {
            if (string.IsNullOrEmpty(LastUsedServer))
            {
                return Servers.FirstOrDefault();
            }

            return Servers.FirstOrDefault(s => s.Name == LastUsedServer);
        }

        public void AddServer(VpnServerConfig server)
        {
            Servers.Add(server);
        }

        public void RemoveServer(string name)
        {
            var server = Servers.FirstOrDefault(s => s.Name == name);
            if (server != null)
            {
                Servers.Remove(server);
            }
        }

        public VpnServerConfig? GetServer(string name)
        {
            return Servers.FirstOrDefault(s => s.Name == name);
        }
    }
}