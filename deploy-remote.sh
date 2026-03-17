#!/bin/bash

set -e

echo "=========================================="
echo "  VPN服务器远程部署脚本"
echo "=========================================="
echo

REMOTE_HOST="127.0.0.1"
REMOTE_PORT="22"
REMOTE_USER="root"
REMOTE_DIR="/root/vpn-server"
VPN_PORT="4080"

echo "配置信息:"
echo "  远程服务器: ${REMOTE_USER}@${REMOTE_HOST}:${REMOTE_PORT}"
echo "  部署目录: ${REMOTE_DIR}"
echo "  VPN端口: ${VPN_PORT}"
echo

echo "开始部署..."

echo "步骤 1/7: 测试SSH连接..."
if ssh -p ${REMOTE_PORT} -o ConnectTimeout=10 -T ${REMOTE_USER}@${REMOTE_HOST} "echo 'SSH连接成功'" > /dev/null 2>&1; then
    echo "✓ SSH连接正常"
else
    echo "✗ SSH连接失败"
    exit 1
fi
echo

echo "步骤 2/7: 清理本地文件..."
echo "清理macOS元数据文件..."
find /Users/sunyu/code/vpn/HubLink.Server -name '._*' -delete
find /Users/sunyu/code/vpn/HubLink.Server -name '.DS_Store' -delete
echo "✓ 本地文件已清理"
echo

echo "步骤 3/7: 清理本地临时文件..."
rm -rf /tmp/vpn-deploy
mkdir -p /tmp/vpn-deploy
echo "✓ 临时目录已创建"
echo

echo "步骤 4/7: 打包项目文件..."
cp Dockerfile /tmp/vpn-deploy/
cp -r HubLink.Server /tmp/vpn-deploy/
cp -r HubLink.Shared /tmp/vpn-deploy/

cd /tmp/vpn-deploy
find . -name '._*' -delete
find . -name '.DS_Store' -delete
echo "✓ 元数据文件已清理"

echo "创建部署包..."
tar -czf /tmp/vpn-server-deploy.tar.gz -C /tmp/vpn-deploy .
echo "✓ 部署包已创建: /tmp/vpn-server-deploy.tar.gz"
echo

echo "步骤 6/7: 上传到远程服务器..."
ssh -p ${REMOTE_PORT} -T ${REMOTE_USER}@${REMOTE_HOST} "mkdir -p ${REMOTE_DIR}"
scp -P ${REMOTE_PORT} /tmp/vpn-server-deploy.tar.gz ${REMOTE_USER}@${REMOTE_HOST}:${REMOTE_DIR}/
echo "✓ 文件上传完成"
echo

echo "步骤 7/7: 在远程服务器构建和部署..."
ssh -p ${REMOTE_PORT} -T ${REMOTE_USER}@${REMOTE_HOST} << ENDSSH
cd ${REMOTE_DIR}
echo "解压部署包..."
tar -xzf vpn-server-deploy.tar.gz
echo "✓ 解压完成"

echo "清理macOS元数据文件..."
find . -name '._*' -delete
find . -name '.DS_Store' -delete
echo "✓ 元数据文件已清理"

echo "停止旧容器..."
docker stop vpn-server 2>/dev/null || true
docker rm vpn-server 2>/dev/null || true
echo "✓ 旧容器已清理"

echo "构建Docker镜像..."
docker build --no-cache -t vpn-server:latest .
if [ \$? -ne 0 ]; then
    echo "✗ 镜像构建失败"
    exit 1
fi
echo "✓ 镜像构建成功"

echo "启动新容器..."
docker run -d \
  --name vpn-server \
  -p ${VPN_PORT}:4080 \
  --restart unless-stopped \
  vpn-server:latest

if [ \$? -ne 0 ]; then
    echo "✗ 容器启动失败"
    exit 1
fi
echo "✓ 容器启动成功"

echo "等待服务器启动..."
sleep 5
ENDSSH
echo

echo "步骤 7/7: 验证部署..."
echo "测试API访问..."
if curl -s --connect-timeout 60 http://${REMOTE_HOST}:${VPN_PORT}/ > /dev/null; then
    echo "✓ API访问正常"
else
    echo "⚠ API访问测试失败，请手动检查"
fi
echo

echo "=========================================="
echo "  部署完成!"
echo "=========================================="
echo
echo "服务器信息:"
echo "  访问地址: http://${REMOTE_HOST}:${VPN_PORT}"
echo "  SignalR Hub: http://${REMOTE_HOST}:${VPN_PORT}/vpnhub"
echo
echo "常用命令:"
echo "  查看日志: ssh -p ${REMOTE_PORT} ${REMOTE_USER}@${REMOTE_HOST} 'docker logs -f vpn-server'"
echo "  重启服务: ssh -p ${REMOTE_PORT} ${REMOTE_USER}@${REMOTE_HOST} 'docker restart vpn-server'"
echo "  停止服务: ssh -p ${REMOTE_PORT} ${REMOTE_USER}@${REMOTE_HOST} 'docker stop vpn-server'"
echo "  查看状态: ssh -p ${REMOTE_PORT} ${REMOTE_USER}@${REMOTE_HOST} 'docker ps | grep vpn-server'"
echo
echo "客户端配置:"
echo "  ServerUrl: http://${REMOTE_HOST}:${VPN_PORT}"
echo "  LocalPort: 1080"
echo "  EncryptionKey: MySecretKey123"
echo

echo "清理临时文件..."
rm -rf /tmp/vpn-deploy
rm -f /tmp/vpn-server-deploy.tar.gz
echo "✓ 清理完成"
