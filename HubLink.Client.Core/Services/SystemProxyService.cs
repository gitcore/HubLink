namespace HubLink.Client.Services;

public enum ProxyType
{
    Web,
    SecureWeb,
    SocksFirewall
}

public partial class SystemProxyService(ILogger<SystemProxyService> logger)
{
    #region Constants

    private const string NetworkSetupCommand = "networksetup";
    private const string BashShell = "/bin/bash";
    private const string EnabledText = "Enabled: Yes";
    private const string ServerKey = "Server:";
    private const string PortKey = "Port:";
    private const string ProxyConfigFileName = "proxy.mobileconfig";
    private const string DisableProxyConfigFileName = "disable_proxy.mobileconfig";

    #endregion

    #region Fields

    private string? _currentService;
    private string? _savedProxyHost;
    private int? _savedProxyPort;
    private bool _wasProxyEnabled;

    #endregion

    #region Public API

    public async Task<bool> EnableProxyAsync(string host, int port, bool autoConfigure = true)
    {
        if (OperatingSystem.IsIOS())
        {
            return await SetIOSProxyAsync(host, port);
        }

        if (OperatingSystem.IsMacOS())
        {
            return await SetMacOSProxyAsync(host, port, autoConfigure);
        }

        if (OperatingSystem.IsLinux())
        {
            return await SetLinuxProxyAsync(host, port);
        }

        logger.LogWarning("System proxy configuration is only supported on macOS, iOS and Linux");
        return false;
    }

    public async Task<bool> DisableProxyAsync()
    {
        var status = await GetProxyStatusAsync();
        
        if (!status.IsEnabled)
        {
            logger.LogInformation("Proxy is not enabled, no need to disable");
            return true;
        }

        if (OperatingSystem.IsIOS())
        {
            return await DisableIOSProxyAsync();
        }

        if (OperatingSystem.IsMacOS())
        {
            return await DisableMacOSProxyAsync();
        }

        if (OperatingSystem.IsLinux())
        {
            return await DisableLinuxProxyAsync();
        }

        return false;
    }

    public async Task<bool> RestoreProxyAsync()
    {
        if (!_wasProxyEnabled || _savedProxyHost == null || _savedProxyPort == null)
        {
            logger.LogWarning("没有保存的代理配置");
            return false;
        }

        logger.LogInformation("恢复代理配置: {Host}:{Port}", _savedProxyHost, _savedProxyPort);
        return await EnableProxyAsync(_savedProxyHost, _savedProxyPort.Value);
    }

    public async Task<ProxyStatus> GetProxyStatusAsync()
    {
        if (OperatingSystem.IsIOS())
        {
            return await GetIOSProxyStatusAsync();
        }

        if (OperatingSystem.IsMacOS())
        {
            return await GetMacOSProxyStatusAsync();
        }

        if (OperatingSystem.IsLinux())
        {
            return await GetLinuxProxyStatusAsync();
        }

        return new ProxyStatus 
        { 
            IsEnabled = false, 
            Message = "System proxy configuration is only supported on macOS, iOS and Linux" 
        };
    }

    public async Task<bool> ConfigurePACFileAsync(string pacUrl)
    {
        if (!OperatingSystem.IsMacOS())
        {
            logger.LogWarning("PAC文件配置仅在macOS上支持");
            return false;
        }

        try
        {
            var services = GetAllNetworkServices();
            
            foreach (var service in services)
            {
                logger.LogInformation("为 {Service} 配置PAC文件: {PacUrl}", service, pacUrl);
                await ExecuteCommandAsync($"{NetworkSetupCommand} -setautoproxyurl {service} {pacUrl}");
                await ExecuteCommandAsync($"{NetworkSetupCommand} -setautoproxystate {service} on");
            }

            logger.LogInformation("PAC文件已配置");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "配置PAC文件失败");
            return false;
        }
    }

    #endregion

    #region macOS Implementation

    private async Task<bool> SetMacOSProxyAsync(string host, int port, bool autoConfigure)
    {
        try
        {
            _currentService = GetActiveNetworkService();
            if (string.IsNullOrEmpty(_currentService))
            {
                logger.LogWarning("未找到活动网络服务");
                return false;
            }

            logger.LogInformation("检测到网络服务: {Service}", _currentService);

            if (autoConfigure)
            {
                await ConfigureAllNetworkServicesAsync(host, port);
            }
            else
            {
                await ConfigureSingleServiceAsync(_currentService, host, port);
            }

            SaveProxyState(host, port, enabled: true);
            logger.LogInformation("系统代理已配置");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "设置代理失败");
            return false;
        }
    }

    private async Task<bool> DisableMacOSProxyAsync()
    {
        try
        {
            var services = GetAllNetworkServices();
            
            foreach (var service in services)
            {
                await DisableProxyForServiceAsync(service);
            }

            _wasProxyEnabled = false;
            logger.LogInformation("系统代理已禁用");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "禁用代理失败");
            return false;
        }
    }

    private async Task<ProxyStatus> GetMacOSProxyStatusAsync()
    {
        try
        {
            var services = GetAllNetworkServices();
            var status = new ProxyStatus { Services = new List<NetworkServiceStatus>() };

            foreach (var service in services)
            {
                var serviceStatus = await GetNetworkServiceProxyStatusAsync(service);
                
                if (serviceStatus.HttpEnabled || serviceStatus.HttpsEnabled || serviceStatus.SocksEnabled)
                {
                    status.Services.Add(serviceStatus);
                }
            }

            status.IsEnabled = status.Services.Any(s => s.HttpEnabled || s.HttpsEnabled || s.SocksEnabled);
            status.Message = status.IsEnabled 
                ? $"已配置 {status.Services.Count} 个网络服务的代理" 
                : "未配置系统代理";

            return status;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取代理状态失败");
            return new ProxyStatus 
            { 
                IsEnabled = false, 
                Message = $"获取代理状态失败: {ex.Message}" 
            };
        }
    }

    private async Task ConfigureAllNetworkServicesAsync(string host, int port)
    {
        var services = GetAllNetworkServices();
        
        foreach (var service in services)
        {
            await ConfigureSingleServiceAsync(service, host, port);
        }
    }

    private async Task ConfigureSingleServiceAsync(string service, string host, int port)
    {
        logger.LogInformation("配置 {Service} 的HTTP代理: {Host}:{Port}", service, host, port);
        await ExecuteCommandAsync($"{NetworkSetupCommand} -setwebproxy {service} {host} {port}");
        
        logger.LogInformation("配置 {Service} 的HTTPS代理: {Host}:{Port}", service, host, port);
        await ExecuteCommandAsync($"{NetworkSetupCommand} -setsecurewebproxy {service} {host} {port}");
        
        logger.LogInformation("启用 {Service} 的HTTP代理", service);
        await ExecuteCommandAsync($"{NetworkSetupCommand} -setwebproxystate {service} on");
        
        logger.LogInformation("启用 {Service} 的HTTPS代理", service);
        await ExecuteCommandAsync($"{NetworkSetupCommand} -setsecurewebproxystate {service} on");
    }

    private async Task DisableProxyForServiceAsync(string service)
    {
        try
        {
            logger.LogInformation("禁用 {Service} 的HTTP代理", service);
            await ExecuteCommandAsync($"{NetworkSetupCommand} -setwebproxystate {service} off");
            
            logger.LogInformation("禁用 {Service} 的HTTPS代理", service);
            await ExecuteCommandAsync($"{NetworkSetupCommand} -setsecurewebproxystate {service} off");
            
            logger.LogInformation("禁用 {Service} 的PAC代理", service);
            await ExecuteCommandAsync($"{NetworkSetupCommand} -setautoproxystate {service} off");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "禁用 {Service} 代理失败", service);
        }
    }

    private static async Task<NetworkServiceStatus> GetNetworkServiceProxyStatusAsync(string service)
    {
        var httpEnabled = await IsProxyEnabledForServiceAsync(service, ProxyType.Web);
        var httpsEnabled = await IsProxyEnabledForServiceAsync(service, ProxyType.SecureWeb);
        var socksEnabled = await IsProxyEnabledForServiceAsync(service, ProxyType.SocksFirewall);
        
        var httpConfig = await GetProxyConfigForServiceAsync(service, ProxyType.Web);
        var httpsConfig = await GetProxyConfigForServiceAsync(service, ProxyType.SecureWeb);
        var socksConfig = await GetProxyConfigForServiceAsync(service, ProxyType.SocksFirewall);
        
        return new NetworkServiceStatus
        {
            ServiceName = service,
            HttpEnabled = httpEnabled,
            HttpsEnabled = httpsEnabled,
            SocksEnabled = socksEnabled,
            HttpHost = httpConfig.Host,
            HttpPort = httpConfig.Port,
            HttpsHost = httpsConfig.Host,
            HttpsPort = httpsConfig.Port,
            SocksHost = socksConfig.Host,
            SocksPort = socksConfig.Port
        };
    }

    private static async Task<bool> IsProxyEnabledForServiceAsync(string service, ProxyType proxyType)
    {
        try
        {
            var result = await ExecuteCommandAsync($"{NetworkSetupCommand} -get{GetProxyTypeString(proxyType)} {service}");
            return result.Contains(EnabledText);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<ProxyConfig> GetProxyConfigForServiceAsync(string service, ProxyType proxyType)
    {
        try
        {
            var result = await ExecuteCommandAsync($"{NetworkSetupCommand} -get{GetProxyTypeString(proxyType)} {service}");
            var lines = result.Split('\n');
            
            var config = new ProxyConfig();
            foreach (var line in lines)
            {
                if (line.Contains(ServerKey))
                {
                    config.Host = line.Split(':').Last().Trim();
                }
                else if (line.Contains(PortKey))
                {
                    var portStr = line.Split(':').Last().Trim();
                    if (int.TryParse(portStr, out int port))
                    {
                        config.Port = port;
                    }
                }
            }
            
            return config;
        }
        catch
        {
            return new ProxyConfig();
        }
    }

    private static string GetActiveNetworkService()
    {
        try
        {
            var result = ExecuteCommand($"{NetworkSetupCommand} -listnetworkserviceorder");
            var lines = result.Split('\n');
            
            foreach (var line in lines)
            {
                var service = line.Trim();
                
                if (string.IsNullOrEmpty(service))
                    continue;
                    
                if (service.Contains("An asterisk") || 
                    service.Contains("denotes") ||
                    service.Contains("network service") ||
                    service.Contains("Hardware Port"))
                {
                    continue;
                }
                
                if (service.StartsWith('('))
                {
                    var match = NetworkServiceRegex().Match(service);
                    if (match.Success)
                    {
                        return match.Groups[2].Value.Trim();
                    }
                }
                
                return service;
            }
            
            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\((\d+)\)\s+(.+)")]
    private static partial System.Text.RegularExpressions.Regex NetworkServiceRegex();

    private static List<string> GetAllNetworkServices()
    {
        try
        {
            var result = ExecuteCommand($"{NetworkSetupCommand} -listallnetworkservices");
            var lines = result.Split('\n');
            var services = new List<string>();
            
            foreach (var line in lines)
            {
                var service = line.Trim();
                if (!string.IsNullOrEmpty(service) && 
                    !service.Contains("Bluetooth") &&
                    !service.Contains("An asterisk") && 
                    !service.Contains("denotes") &&
                    !service.Contains("network service"))
                {
                    services.Add(service);
                }
            }
            
            return services;
        }
        catch
        {
            return new List<string>();
        }
    }

    #endregion

    #region iOS Implementation

    private async Task<bool> SetIOSProxyAsync(string host, int port)
    {
        try
        {
            var configContent = GenerateIOSProxyConfig(host, port);
            var configPath = Path.Combine(Path.GetTempPath(), ProxyConfigFileName);
            
            await File.WriteAllTextAsync(configPath, configContent);
            
            SaveProxyState(host, port, enabled: true);
            
            logger.LogInformation("iOS代理配置文件已生成: {Path}", configPath);
            logger.LogInformation("请通过系统设置或MDM安装此配置文件以启用代理");
            
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "生成iOS代理配置失败");
            return false;
        }
    }

    private async Task<bool> DisableIOSProxyAsync()
    {
        try
        {
            var configContent = GenerateIOSProxyConfig("", 0, enabled: false);
            var configPath = Path.Combine(Path.GetTempPath(), DisableProxyConfigFileName);
            
            await File.WriteAllTextAsync(configPath, configContent);
            
            _wasProxyEnabled = false;
            
            logger.LogInformation("iOS代理禁用配置文件已生成: {Path}", configPath);
            logger.LogInformation("请通过系统设置或MDM安装此配置文件以禁用代理");
            
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "生成iOS代理禁用配置失败");
            return false;
        }
    }

    private async Task<ProxyStatus> GetIOSProxyStatusAsync()
    {
        try
        {
            var status = new ProxyStatus 
            { 
                IsEnabled = _wasProxyEnabled,
                Message = _wasProxyEnabled 
                    ? $"iOS代理已配置: {_savedProxyHost}:{_savedProxyPort}" 
                    : "iOS代理未配置",
                Services = new List<NetworkServiceStatus>()
            };

            if (_wasProxyEnabled && _savedProxyHost != null && _savedProxyPort != null)
            {
                status.Services.Add(new NetworkServiceStatus
                {
                    ServiceName = "iOS System Proxy",
                    HttpEnabled = true,
                    HttpsEnabled = true,
                    HttpHost = _savedProxyHost,
                    HttpPort = _savedProxyPort,
                    HttpsHost = _savedProxyHost,
                    HttpsPort = _savedProxyPort
                });
            }

            return status;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取iOS代理状态失败");
            return new ProxyStatus 
            { 
                IsEnabled = false, 
                Message = $"获取iOS代理状态失败: {ex.Message}" 
            };
        }
    }

    private static string GenerateIOSProxyConfig(string host, int port, bool enabled = true)
    {
        var uuid = Guid.NewGuid().ToString();
        var payloadUuid = Guid.NewGuid().ToString();
        var enabledValue = enabled ? 1 : 0;
        
        return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>PayloadContent</key>
    <array>
        <dict>
            <key>PayloadDescription</key>
            <string>Configures HTTP and HTTPS proxy settings</string>
            <key>PayloadDisplayName</key>
            <string>Proxy Configuration</string>
            <key>PayloadIdentifier</key>
            <string>com.apple.proxy.managed.{uuid}</string>
            <key>PayloadOrganization</key>
            <string>HubLink</string>
            <key>PayloadType</key>
            <string>com.apple.proxy.manual</string>
            <key>PayloadUUID</key>
            <string>{uuid}</string>
            <key>PayloadVersion</key>
            <integer>1</integer>
            <key>HTTPEnable</key>
            <integer>{enabledValue}</integer>
            <key>HTTPPort</key>
            <integer>{port}</integer>
            <key>HTTPProxy</key>
            <string>{host}</string>
            <key>HTTPSEnable</key>
            <integer>{enabledValue}</integer>
            <key>HTTPSPort</key>
            <integer>{port}</integer>
            <key>HTTPSProxy</key>
            <string>{host}</string>
        </dict>
    </array>
    <key>PayloadDescription</key>
    <string>HubLink Proxy Configuration</string>
    <key>PayloadDisplayName</key>
    <string>HubLink Proxy</string>
    <key>PayloadIdentifier</key>
    <string>com.hublink.proxy.{uuid}</string>
    <key>PayloadOrganization</key>
    <string>HubLink</string>
    <key>PayloadRemovalDisallowed</key>
    <false/>
    <key>PayloadType</key>
    <string>Configuration</string>
    <key>PayloadUUID</key>
    <string>{payloadUuid}</string>
    <key>PayloadVersion</key>
    <integer>1</integer>
</dict>
</plist>";
    }

    #endregion

    #region Linux Implementation

    private async Task<bool> SetLinuxProxyAsync(string host, int port)
    {
        try
        {
            var proxyUrl = $"http://{host}:{port}";
            
            logger.LogInformation("Setting Linux proxy: {ProxyUrl}", proxyUrl);
            
            await ExecuteCommandAsync($"export http_proxy=\"{proxyUrl}\"");
            await ExecuteCommandAsync($"export https_proxy=\"{proxyUrl}\"");
            await ExecuteCommandAsync($"export HTTP_PROXY=\"{proxyUrl}\"");
            await ExecuteCommandAsync($"export HTTPS_PROXY=\"{proxyUrl}\"");
            await ExecuteCommandAsync($"export all_proxy=\"{proxyUrl}\"");
            await ExecuteCommandAsync($"export ALL_PROXY=\"{proxyUrl}\"");
            
            SaveProxyState(host, port, enabled: true);
            logger.LogInformation("Linux proxy has been configured");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set Linux proxy");
            return false;
        }
    }

    private async Task<bool> DisableLinuxProxyAsync()
    {
        try
        {
            logger.LogInformation("Disabling Linux proxy");
            
            await ExecuteCommandAsync("unset http_proxy");
            await ExecuteCommandAsync("unset https_proxy");
            await ExecuteCommandAsync("unset HTTP_PROXY");
            await ExecuteCommandAsync("unset HTTPS_PROXY");
            await ExecuteCommandAsync("unset all_proxy");
            await ExecuteCommandAsync("unset ALL_PROXY");
            
            _wasProxyEnabled = false;
            logger.LogInformation("Linux proxy has been disabled");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to disable Linux proxy");
            return false;
        }
    }

    private async Task<ProxyStatus> GetLinuxProxyStatusAsync()
    {
        try
        {
            var httpProxy = Environment.GetEnvironmentVariable("http_proxy") ?? 
                          Environment.GetEnvironmentVariable("HTTP_PROXY");
            var httpsProxy = Environment.GetEnvironmentVariable("https_proxy") ?? 
                           Environment.GetEnvironmentVariable("HTTPS_PROXY");
            var allProxy = Environment.GetEnvironmentVariable("all_proxy") ?? 
                          Environment.GetEnvironmentVariable("ALL_PROXY");
            
            var isEnabled = !string.IsNullOrEmpty(httpProxy) || 
                          !string.IsNullOrEmpty(httpsProxy) || 
                          !string.IsNullOrEmpty(allProxy);
            
            var status = new ProxyStatus
            {
                IsEnabled = isEnabled,
                Message = isEnabled 
                    ? "Linux proxy is configured" 
                    : "Linux proxy is not configured",
                Services = new List<NetworkServiceStatus>()
            };

            if (isEnabled && _savedProxyHost != null && _savedProxyPort != null)
            {
                status.Services.Add(new NetworkServiceStatus
                {
                    ServiceName = "Linux Environment Proxy",
                    HttpEnabled = !string.IsNullOrEmpty(httpProxy),
                    HttpsEnabled = !string.IsNullOrEmpty(httpsProxy),
                    HttpHost = _savedProxyHost,
                    HttpPort = _savedProxyPort,
                    HttpsHost = _savedProxyHost,
                    HttpsPort = _savedProxyPort
                });
            }

            return status;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get Linux proxy status");
            return new ProxyStatus
            {
                IsEnabled = false,
                Message = $"Failed to get Linux proxy status: {ex.Message}"
            };
        }
    }

    #endregion

    #region Helper Methods

    private void SaveProxyState(string host, int port, bool enabled)
    {
        _savedProxyHost = host;
        _savedProxyPort = port;
        _wasProxyEnabled = enabled;
    }

    private static string GetProxyTypeString(ProxyType proxyType)
    {
        return proxyType switch
        {
            ProxyType.Web => "webproxy",
            ProxyType.SecureWeb => "securewebproxy",
            ProxyType.SocksFirewall => "socksfirewallproxy",
            _ => "webproxy"
        };
    }

    private static ProcessStartInfo CreateProcessStartInfo(string command)
    {
        return new ProcessStartInfo
        {
            FileName = BashShell,
            Arguments = $"-c \"{command}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }

    private static async Task<string> ExecuteCommandAsync(string command)
    {
        var process = new Process { StartInfo = CreateProcessStartInfo(command) };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0 && !string.IsNullOrEmpty(error))
        {
            throw new Exception(error);
        }

        return output.Trim();
    }

    private static string ExecuteCommand(string command)
    {
        var process = new Process { StartInfo = CreateProcessStartInfo(command) };

        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0 && !string.IsNullOrEmpty(error))
        {
            throw new Exception(error);
        }

        return output.Trim();
    }

    #endregion
}

public class ProxyStatus
{
    public bool IsEnabled { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<NetworkServiceStatus> Services { get; set; } = new();
}

public class NetworkServiceStatus
{
    public string ServiceName { get; set; } = string.Empty;
    public bool HttpEnabled { get; set; }
    public bool HttpsEnabled { get; set; }
    public bool SocksEnabled { get; set; }
    public string? HttpHost { get; set; }
    public int? HttpPort { get; set; }
    public string? HttpsHost { get; set; }
    public int? HttpsPort { get; set; }
    public string? SocksHost { get; set; }
    public int? SocksPort { get; set; }
}

public class ProxyConfig
{
    public string Host { get; set; } = string.Empty;
    public int? Port { get; set; }
}