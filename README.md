# KmaoLink

轻量级 Windows 桌面蓝牙管理悬浮窗工具，支持快速开关蓝牙、设备连接切换。

## 功能特性

- 悬浮窗显示蓝牙状态
- 一键开关蓝牙
- 显示已配对设备列表
- 快速连接/断开设备
- 窗口可拖动，位置自动记忆
- 支持开机自启
- 兼容 Windows 10+ (原生API) 和 Windows 7/8 (命令行工具)

## 界面说明

### 紧凑模式 (60x60)
- 显示蓝牙图标
- 蓝色 = 已开启，灰色 = 已关闭
- 点击图标 = 快速开关蓝牙
- 单击窗口 = 展开设备列表

### 展开模式 (280x可变高度)
- 显示蓝牙状态和开关按钮
- 显示已配对设备列表
- 每个设备显示连接状态
- 点击"连接"/"断开"切换设备

## 编译方法

### 前置要求
- .NET 8.0 SDK
- Windows 10 或更高版本（开发环境）

### 编译步骤

```bash
# 进入项目目录
cd KmaoLink

# 还原依赖
dotnet restore

# 编译
dotnet build -c Release

# 发布为单文件
dotnet publish -c Release -r win-x64 --self-contained false
```

### 生成的文件
发布后在 `bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/` 目录下会生成：
- `KmaoLink.exe` - 主程序（约 5-10MB）

## 使用方法

1. 运行 `KmaoLink.exe`
2. 程序会在屏幕右下角显示一个小蓝牙图标
3. 点击图标可以快速开关蓝牙
4. 单击窗口展开设备列表
5. 在设备列表中点击"连接"/"断开"管理设备
6. 右键系统托盘图标可以设置开机自启

## 兼容性说明

### Windows 10/11
- 使用 PowerShell + Windows Bluetooth API
- 功能完整

### Windows 7/8
- 需要下载 `bluetoothCL.exe` 放在程序同目录
- 下载地址: https://www.nirsoft.net/utils/bluetooth_command_line.html

## 配置文件

设置保存在: `%AppData%/KmaoLink/settings.json`

```json
{
  "WindowX": 1860,
  "WindowY": 1020,
  "AutoStart": true,
  "StartMinimized": true,
  "RefreshInterval": 5000
}
```

## 注意事项

1. 部分功能需要**管理员权限**才能控制蓝牙硬件
2. 首次运行时如果被 Windows Defender 拦截，请选择"仍要运行"
3. 如果遇到权限问题，右键 exe -> 属性 -> 勾选"以管理员身份运行"

## 项目结构

```
KmaoLink/
├── KmaoLink.csproj               # 项目配置
├── Program.cs                    # 入口文件
├── MainForm.cs                   # 主窗体（悬浮窗UI）
├── BluetoothManager.cs           # 蓝牙管理核心
├── AutoStartManager.cs           # 开机自启管理
├── SettingsManager.cs            # 设置管理
├── logo.png                      # 应用图标
└── logo.ico                      # 转换后的图标（exe 用）
```

## 开源协议

MIT License
