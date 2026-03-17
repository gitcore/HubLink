# HubLink - 快速开始指南

## 项目简介

HubLink是一个高性能的VPN应用程序，使用.NET 10.0构建，支持SignalR和WebSocket传输协议，在客户端和服务器之间创建加密隧道。

## 启动VPN服务器

### 方式1: 使用Docker（推荐）

```bash
docker-compose up -d
```

服务器将启动在 `http://localhost:4080`

### 方式2: 使用.NET CLI

```bash
cd HubLink.Server
dotnet run
```

服务器将启动在 `http://localhost:4080`

## 启动VPN客户端

```bash
cd HubLink.Client
dotnet run
```

客户端将启动Web界面，访问 `http://localhost:5080` 进行管理。

## 使用Web界面

### 访问界面

在浏览器中打开 `http://localhost:5080`

### 主要功能

#### 连接管理
- **连接按钮**: 点击 `[ CONNECT ]` 连接到VPN服务器
- **断开按钮**: 点击 `[ DISCONNECT ]` 断开连接
- **状态监控**: 实时显示连接状态、服务器信息、运行时间等

#### 流量统计
- **上传流量**: 显示总上传数据量
- **下载流量**: 显示总下载数据量
- **上传速度**: 实时上传速度
- **下载速度**: 实时下载速度
- **连接数**: 当前活跃的连接数量

#### 服务器管理
- **服务器列表**: 显示所有配置的VPN服务器
- **添加服务器**: 添加新的VPN服务器配置
- **删除服务器**: 删除不需要的服务器配置
- **选择服务器**: 点击服务器项进行切换

### 添加服务器

1. 点击界面上的 `[ ADD SERVER ]` 按钮
2. 填写服务器信息：
   ```
   服务器名称: 我的VPN服务器
   服务器URL: http://localhost:4080
   本地端口: 1080
   加密密钥: MySecretKey123
   启用加密: false
   传输类型: SignalR
   自动重连: true
   重连间隔: 5000
   自动代理: true
   ```
3. 点击 `[ SAVE ]` 保存配置

### 连接VPN

1. 在服务器列表中选择要连接的服务器
2. 点击 `[ CONNECT ]` 按钮
3. 等待连接成功
4. 状态会显示为 `CONNECTED`

### 断开VPN

1. 点击 `[ DISCONNECT ]` 按钮
2. 等待断开完成
3. 状态会显示为 `DISCONNECTED`

## 测试VPN

### 方法1: 使用curl

```bash
curl -x http://127.0.0.1:1080 http://www.baidu.com
```

### 方法2: 配置浏览器

#### Chrome/Edge
1. 安装 "Proxy SwitchyOmega" 扩展
2. 配置代理：
   - 协议: HTTP
   - 服务器: 127.0.0.1
   - 端口: 1080
3. 访问 http://www.baidu.com

#### Firefox
1. 设置 → 网络设置
2. 选择 "手动配置代理"
3. HTTP代理: 127.0.0.1:1080
4. 勾选"也将此代理用于HTTPS"

#### Safari
1. 系统设置 → 网络
2. 选择当前网络 → 代理
3. HTTP代理: 127.0.0.1:1080
4. 勾选"代理服务器"

## 配置文件

服务器配置自动保存在 `vpn-config.json`：

```json
{
  "Servers": [
    {
      "Name": "我的VPN服务器",
      "ServerUrl": "http://localhost:4080",
      "LocalPort": 1080,
      "EncryptionKey": "MySecretKey123",
      "EnableEncryption": false,
      "AutoReconnect": true,
      "ReconnectInterval": 5000,
      "AutoProxy": true
    }
  ],
  "LastUsedServer": "我的VPN服务器"
}
```

## 传输协议

### SignalR
- 实时双向通信
- 自动重连
- 适合不稳定网络环境

### WebSocket
- 更低的开销
- 直接WebSocket连接
- 适合高性能场景

## Web界面特性

### 实时状态监控
- 连接状态（已连接/已断开）
- 当前服务器信息
- 运行时间统计
- 重连次数统计
- 系统代理状态

### 流量统计
- 实时上传/下载流量
- 实时上传/下载速度
- 活跃连接数量
- 像素风格界面设计

### 服务器管理
- 添加/删除服务器
- 服务器列表显示
- 快速切换服务器
- 服务器状态指示

## 部署

### Docker部署

#### 开发环境
```bash
docker-compose up -d
```

#### 生产环境
```bash
docker-compose -f docker-compose.prod.yml up -d
```

### 远程部署
```bash
./deploy-remote.sh
```

**注意**: 部署脚本使用示例配置（127.0.0.1:22）。生产使用时，请修改脚本中的实际服务器详情。

## 常见问题

### Q: 无法访问Web界面？
A:
1. 确保VPN客户端正在运行
2. 检查端口5080是否被占用
3. 查看客户端日志

### Q: 连接失败怎么办？
A:
1. 确保VPN服务器正在运行
2. 检查服务器URL是否正确（默认端口4080）
3. 查看Web界面中的错误信息
4. 尝试切换传输协议

### Q: 浏览器无法访问？
A:
1. 确保VPN已连接
2. 检查浏览器代理设置
3. 使用curl测试代理是否工作
4. 检查本地端口1080是否被占用

### Q: 如何查看流量统计？
A:
1. 访问Web界面 http://localhost:5080
2. 在"TRAFFIC STATS"部分查看实时统计

### Q: 如何配置多个服务器？
A:
1. 在Web界面中点击 `[ ADD SERVER ]`
2. 填写新的服务器信息
3. 点击 `[ SAVE ]` 保存
4. 可以在服务器列表中看到所有服务器

### Q: 如何选择传输协议？
A:
1. 在添加服务器时选择传输类型
2. SignalR: 适合不稳定网络，自动重连
3. WebSocket: 适合高性能场景，低开销

### Q: Web界面不更新？
A:
1. 刷新浏览器页面
2. 检查客户端API是否正常运行
3. 查看浏览器控制台错误信息

## 界面说明

### 状态卡片
显示当前VPN连接状态，包括：
- 连接状态
- 服务器地址
- 端口信息
- 运行时间
- 重连次数
- 代理状态

### 流量卡片
显示实时流量统计，包括：
- 上传/下载总量
- 上传/下载速度
- 活跃连接数

### 服务器列表
显示所有配置的服务器：
- 服务器名称和URL
- 连接状态指示
- 删除按钮
- 点击选择服务器

## 下一步

- 查看完整文档: [README.md](README.md)
- 测试VPN: 使用curl或浏览器
- 添加更多服务器: 使用Web界面添加功能
- 访问Web界面: http://localhost:5080
- 运行测试: `dotnet test HubLink.Test`
