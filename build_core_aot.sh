#!/bin/bash

echo "=== 编译 HubLink.Client.Core 为 AOT dylib ==="

# 清理旧的构建
echo "清理旧的构建..."
rm -rf HubLink.Client.Core/bin/Release/net10.0/osx-arm64/publish

# 编译为 AOT dylib
echo "编译为 AOT dylib..."
dotnet publish HubLink.Client.Core/HubLink.Client.Core.csproj \
    -c Release \
    -r osx-arm64 \
    --self-contained \
    -p:PublishAot=true \
    -p:PublishTrimmed=true \
    -p:NativeLib=Shared

if [ $? -eq 0 ]; then
    echo "✅ 编译成功！"
    
    # 复制 dylib 到项目根目录
    cp HubLink.Client.Core/bin/Release/net10.0/osx-arm64/publish/HubLink.Client.Core.dylib ./HubLink.Client.Core.dylib
    
    echo "dylib 位置: ./HubLink.Client.Core.dylib"
else
    echo "❌ 编译失败！"
    exit 1
fi
