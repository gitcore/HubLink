#!/bin/bash

echo "=== 编译 HubLink.Client 为非 AOT 可执行文件 ==="

# 清理旧的构建
echo "清理旧的构建..."
rm -rf HubLink.Client/bin/Debug/net10.0/osx-arm64

# 编译为非 AOT 可执行文件
echo "编译为非 AOT 可执行文件..."
dotnet build HubLink.Client/HubLink.Client.csproj \
    -c Debug \
    -r osx-arm64

if [ $? -eq 0 ]; then
    echo "✅ 编译成功！"
    
    echo "可执行文件位置: HubLink.Client/bin/Debug/net10.0/osx-arm64/HubLink.Client"
else
    echo "❌ 编译失败！"
    exit 1
fi
