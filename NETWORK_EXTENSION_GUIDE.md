# Network Extension Framework 集成指南

本文档说明如何将 Network Extension Framework 与 AOT 库集成，实现完整的 macOS/iOS VPN 功能。

## 架构概述

```
┌─────────────────────────────────────────────────────────────┐
│                     iOS/macOS App                           │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │   SwiftUI    │  │  Network     │  │  AOT Native  │      │
│  │    UI        │◄─┤  Extension   │◄─┤   Library    │      │
│  │              │  │  (Packet     │  │  (C# Core)   │      │
│  │ VpnManager   │  │   Tunnel)    │  │              │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
                   ┌────────────────┐
                   │  VPN Server    │
                   │  (SignalR)     │
                   └────────────────┘
```

## 文件说明

### 1. PacketTunnelProvider.swift
Packet Tunnel Extension 的核心实现，负责：
- 启动和停止 VPN 隧道
- 初始化 AOT 库
- 处理网络数据包
- 与主应用通信

### 2. VpnManager.swift
主应用的 VPN 管理器，负责：
- 管理 NETunnelProviderManager
- 启动和停止 VPN 连接
- 监控连接状态
- 获取流量统计信息

### 3. ContentView.swift
SwiftUI 用户界面，提供：
- 连接/断开按钮
- 服务器选择
- 状态显示
- 流量统计

### 4. PacketTunnel-Info.plist
Packet Tunnel Extension 的配置文件，定义：
- Extension 类型
- 主类名称
- Bundle 信息

### 5. PacketTunnel.entitlements
权限配置文件，包含：
- Network Extension 权限
- App Group 权限（用于数据共享）

## Xcode 项目配置

### 步骤 1: 添加 Packet Tunnel Extension Target

1. 在 Xcode 中，选择 File > New > Target
2. 选择 "Network Extension" 模板
3. 选择 "Packet Tunnel Provider" 类型
4. 命名为 "PacketTunnel"

### 步骤 2: 配置 Packet Tunnel Extension

1. 将 `PacketTunnelProvider.swift` 添加到 Packet Tunnel target
2. 将 `VpnClient.swift` 添加到 Packet Tunnel target
3. 将 `HubLink.Client.dylib` 添加到 Packet Tunnel target 的 Frameworks
4. 将 `vpn-config.json` 添加到 Packet Tunnel target 的 Resources

### 步骤 3: 配置 Entitlements

1. 在 Packet Tunnel target 的 Build Settings 中：
   - 设置 "Code Signing Entitlements" 为 `PacketTunnel.entitlements`
2. 在主应用的 Build Settings 中：
   - 设置 "Code Signing Entitlements" 为 `HubLink.SwiftUI.entitlements`

### 步骤 4: 配置 App Groups

1. 在 Apple Developer Portal 中创建 App Group:
   - Group ID: `group.com.hublink.vpn`
2. 在两个 target 的 Entitlements 文件中添加此 App Group

### 步骤 5: 配置 Bundle Identifiers

确保 Bundle Identifiers 符合以下格式：
- 主应用: `com.hublink.vpn`
- Packet Tunnel: `com.hublink.vpn.PacketTunnel`

## 使用说明

### 启动 VPN 连接

```swift
let vpnManager = VpnManager()
vpnManager.connect(serverName: "Remote Server")
```

### 断开 VPN 连接

```swift
vpnManager.disconnect()
```

### 获取状态

```swift
vpnManager.getStatus()
```

### 监控流量统计

VpnManager 会自动更新流量统计信息，包括：
- 上传速度
- 下载速度
- 运行时间
- 活动连接数

## 数据流

### 连接流程

1. 用户点击连接按钮
2. VpnManager 调用 NETunnelProviderManager
3. PacketTunnelProvider 启动
4. 初始化 AOT 库
5. 调用 AOT 库的 connect 方法
6. 建立到 VPN 服务器的连接
7. 开始处理网络数据包

### 数据包处理流程

1. Network Extension 拦截网络数据包
2. PacketTunnelProvider 接收数据包
3. 通过 AOT 库转发到 VPN 服务器
4. VPN 服务器处理后返回
5. 数据包返回到应用层

### 状态监控流程

1. VpnManager 定期发送消息到 PacketTunnelProvider
2. PacketTunnelProvider 调用 AOT 库获取状态
3. 返回状态信息到 VpnManager
4. VpnManager 更新 UI 显示

## 注意事项

### 开发要求

1. **Apple Developer 账号**: 必须有付费开发者账号
2. **签名证书**: 需要配置正确的开发者证书
3. **Provisioning Profile**: 需要包含 Network Extension 权限

### App Store 审核

1. VPN 应用需要特殊审核
2. 需要提供详细的使用说明
3. 需要说明数据隐私政策

### 性能优化

1. 数据包处理需要高性能实现
2. 避免在数据包处理路径中进行阻塞操作
3. 使用异步处理提高性能

### 错误处理

1. 完善的错误处理和重连机制
2. 提供用户友好的错误提示
3. 记录详细的日志信息

## 调试

### 日志输出

使用 Console.app 查看系统日志：
- Packet Tunnel 日志: `log stream --predicate 'subsystem == "com.hublink.vpn"'`
- 主应用日志: `log stream --predicate 'subsystem == "com.hublink.vpn.app"'`

### 常见问题

1. **连接失败**: 检查网络配置和服务器地址
2. **权限错误**: 确认 Entitlements 配置正确
3. **签名错误**: 检查证书和 Provisioning Profile

## 下一步

1. 完善数据包处理逻辑
2. 添加更多错误处理
3. 实现自动重连机制
4. 添加流量限制功能
5. 实现多服务器负载均衡

## 参考资料

- [Apple Network Extension Framework](https://developer.apple.com/documentation/networkextension)
- [NEPacketTunnelProvider](https://developer.apple.com/documentation/networkextension/nepackettunnelprovider)
- [NETunnelProviderManager](https://developer.apple.com/documentation/networkextension/netunnelprovidermanager)