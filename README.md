# HubLink

A high-performance VPN-like application built with .NET 10.0, featuring SignalR and WebSocket support for creating encrypted tunnels between clients and servers.

## Features

- **Multiple Transport Protocols**: Support for both SignalR and WebSocket connections
- **AES Encryption**: Secure data transmission with configurable encryption keys
- **Cross-Platform**: Runs on macOS, Linux, and Windows
- **Docker Support**: Easy deployment with Docker and Docker Compose
- **Web Interface**: Built-in web dashboard for monitoring and management
- **System Proxy Integration**: Automatic proxy configuration on macOS
- **Traffic Statistics**: Real-time monitoring of upload/download traffic
- **Multi-Server Management**: Support for managing multiple VPN server configurations
- **Auto-Reconnection**: Automatic reconnection with configurable intervals
- **High Performance**: Optimized for handling multiple concurrent connections

## Architecture

### Server Side (HubLink.Server)
- ASP.NET Core Web API with SignalR Hub
- WebSocket connection handler for alternative transport
- TCP connection pooling for efficient resource management
- DNS resolution service
- Real-time traffic statistics tracking
- Docker containerization support

### Client Side (HubLink.Client)
- Console application with interactive menu
- Local proxy server (configurable port)
- System proxy integration (macOS)
- Multiple VPN server management
- Real-time connection status monitoring
- Traffic statistics display

### Core Library (HubLink.Client.Core)
- VPN client implementation
- Tunnel transport abstraction (SignalR/WebSocket)
- System proxy service
- Tunnel connection management
- Configuration models and serialization

### Shared Library (HubLink.Shared)
- Common data structures and utilities
- Packet handling and encryption
- SOCKS5 helper functions
- Traffic statistics interfaces
- Tunnel channel management

## Project Structure

```
HubLink/
├── HubLink.Server/              # Server application
│   ├── Hubs/
│   │   └── VpnHub.cs          # SignalR Hub for client communication
│   ├── Services/
│   │   ├── VpnTunnelService.cs       # Tunnel management service
│   │   ├── TcpConnectionPool.cs      # Connection pooling
│   │   ├── DnsResolver.cs            # DNS resolution
│   │   ├── ServerTrafficStats.cs     # Traffic statistics
│   │   └── WebSocketConnectionHandler.cs  # WebSocket handler
│   ├── Program.cs                   # Server entry point
│   └── appsettings.json             # Server configuration
├── HubLink.Client/              # Client application
│   ├── Services/
│   │   └── VpnClientApiService.cs   # API service for client
│   ├── wwwroot/
│   │   └── index.html               # Web dashboard
│   ├── Program.cs                   # Client entry point
│   ├── appsettings.json             # Client configuration
│   └── vpn-config.json              # VPN server configurations
├── HubLink.Client.Core/        # Core client library
│   ├── Models/
│   │   ├── ApiModels.cs            # API data models
│   │   ├── ConnectionStatus.cs      # Connection status
│   │   ├── TrafficStats.cs         # Traffic statistics
│   │   └── VpnConfig.cs            # VPN configuration
│   ├── Services/
│   │   ├── VpnClientService.cs      # Main VPN client service
│   │   ├── SignalRTunnelTransport.cs    # SignalR transport
│   │   ├── WebSocketTunnelTransport.cs  # WebSocket transport
│   │   ├── TunnelConnectionService.cs  # Connection management
│   │   └── SystemProxyService.cs       # System proxy integration
│   ├── IVpnClient.cs               # VPN client interface
│   ├── VpnClient.cs                # VPN client implementation
│   └── VpnClientMode.cs            # Client modes
├── HubLink.Shared/              # Shared library
│   ├── ITunnelTransport.cs        # Transport interface
│   ├── ITrafficStats.cs          # Traffic statistics interface
│   ├── TunnelChannel.cs           # Tunnel channel
│   ├── VpnPacket.cs              # VPN packet
│   ├── VpnPacketHelper.cs        # Packet utilities
│   ├── VpnClientTunnel.cs        # Client tunnel
│   ├── VpnClientTunnelManager.cs # Tunnel manager
│   ├── TunnelInfo.cs             # Tunnel information
│   ├── AesCryptoPool.cs          # AES encryption pool
│   ├── Socks5Helper.cs           # SOCKS5 utilities
│   └── HttpHelper.cs            # HTTP utilities
├── HubLink.Test/               # Test project
│   ├── AesCryptoPoolTest.cs     # Encryption tests
│   ├── HttpHelperTests.cs       # HTTP helper tests
│   └── VpnPacketTest.cs        # Packet tests
├── docs/                       # Documentation
│   ├── CROSS_PLATFORM_ARCHITECTURE.md
│   ├── CROSS_PLATFORM_ARCHITECTURE_V2.md
│   ├── CROSS_PLATFORM_ARCHITECTURE_V3.md
│   └── SWIFT_BINDING_GUIDE.md
├── Dockerfile                  # Server Docker image
├── docker-compose.yml          # Development compose
├── docker-compose.prod.yml     # Production compose
├── deploy-remote.sh           # Remote deployment script
├── build_client.sh            # Client build script
├── build_core_aot.sh         # Core AOT build script
└── HubLink.sln               # Solution file
```

## Prerequisites

- .NET 10.0 SDK or later
- Docker (for containerized deployment)
- macOS, Linux, or Windows

## Quick Start

### Building the Project

```bash
dotnet build
```

### Running the Server

#### Option 1: Using Docker (Recommended)

```bash
docker-compose up -d
```

#### Option 2: Using .NET CLI

```bash
cd HubLink.Server
dotnet run
```

The server will start on `http://localhost:4080`

### Running the Client

#### Option 1: Simple Mode

```bash
cd HubLink.Client
dotnet run
```

Select option 1 for simple mode with default configuration.

#### Option 2: Advanced Mode

```bash
cd HubLink.Client
dotnet run
```

Select option 2 for advanced mode with:
- Multiple server management
- Traffic statistics
- System proxy configuration
- Connection status monitoring

## Configuration

### Server Configuration (HubLink.Server/appsettings.json)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://+:4080"
      }
    }
  },
  "Vpn": {
    "ApiKey": "your-secret-api-key-change-this-in-production",
    "TransportType": "SignalR"
  }
}
```

### Client Configuration (HubLink.Client/vpn-config.json)

```json
{
  "Servers": [
    {
      "Name": "My VPN Server",
      "ServerUrl": "http://localhost:4080",
      "LocalPort": 1080,
      "EncryptionKey": "MySecretKey123",
      "EnableEncryption": false,
      "AutoReconnect": true,
      "ReconnectInterval": 5000,
      "AutoProxy": true
    }
  ],
  "LastUsedServer": "My VPN Server"
}
```

## Deployment

### Docker Deployment

#### Development

```bash
docker-compose up -d
```

#### Production

```bash
docker-compose -f docker-compose.prod.yml up -d
```

### Remote Deployment

Use the deployment script to deploy to a remote server:

```bash
./deploy-remote.sh
```

**Note**: The deployment script uses example configuration (127.0.0.1:22). For production use, modify the script with your actual server details.

## How It Works

1. **Connection**: Client connects to VPN server using SignalR or WebSocket
2. **Local Proxy**: Client starts a TCP listener on configured local port (default: 1080)
3. **Data Flow**:
   - Local application connects to client's local proxy
   - Client receives data and optionally encrypts it using AES
   - Data is sent to VPN server via SignalR/WebSocket
   - Server forwards to target server
   - Server receives response and sends back to client
   - Client decrypts (if encrypted) and forwards to local application

## Security

- AES-256 encryption support for data transmission
- Configurable encryption keys
- API key authentication for server access
- Secure WebSocket connections (can be upgraded to HTTPS/WSS)

## Testing

Run the test suite:

```bash
dotnet test HubLink.Test
```

## Documentation

- [Quick Start Guide](QUICKSTART.md) - Detailed client usage guide
- [Network Extension Guide](NETWORK_EXTENSION_GUIDE.md) - Network extension integration
- [Cross-Platform Architecture](docs/CROSS_PLATFORM_ARCHITECTURE.md) - Architecture overview
- [Swift Binding Guide](docs/SWIFT_BINDING_GUIDE.md) - Swift integration guide

## Advanced Features

### Transport Types

- **SignalR**: Real-time bidirectional communication with automatic reconnection
- **WebSocket**: Lower overhead, direct WebSocket connection

### Client Modes

- **Simple Mode**: Quick connection with default settings
- **Advanced Mode**: Full-featured interface with server management

### System Proxy (macOS)

Automatic system proxy configuration:
- Enable/disable system proxy
- View proxy status
- Automatic configuration based on VPN settings

### Traffic Statistics

Real-time monitoring:
- Upload/download traffic
- Connection speed
- Active tunnel count

## Development

### Building Client Core as AOT

```bash
./build_core_aot.sh
```

### Building Client

```bash
./build_client.sh
```

### Adding New Features

The project is structured for easy extension:
- Add new Hub methods in [VpnHub.cs](HubLink.Server/Hubs/VpnHub.cs)
- Implement new tunnel logic in [VpnTunnelService.cs](HubLink.Server/Services/VpnTunnelService.cs)
- Extend client functionality in [VpnClientService.cs](HubLink.Client.Core/Services/VpnClientService.cs)
- Add new transport types by implementing [ITunnelTransport](HubLink.Shared/ITunnelTransport.cs)

## License

This project is provided as-is for educational and development purposes.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
