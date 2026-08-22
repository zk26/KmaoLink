# KmaoLink

轻量级 Windows 桌面蓝牙管理悬浮窗工具，支持快速开关蓝牙、设备连接切换。

## 下载

[![Release](https://img.shields.io/github/v/release/zk26/KmaoLink)](https://github.com/zk26/KmaoLink/releases/latest)

👉 [点击下载最新版 KmaoLink.exe](https://github.com/zk26/KmaoLink/releases/latest)

无需安装，双击即可运行（需 .NET 8.0 Runtime）。

## 功能特性

- 悬浮窗显示蓝牙状态
- 一键开关蓝牙
- 显示已配对设备列表
- 快速连接/断开设备
- 窗口可拖动，位置自动记忆
- 支持开机自启
- 兼容 Windows 10+（原生 API）和 Windows 7/8（命令行工具）

## 使用方法

1. 运行 `KmaoLink.exe`
2. 程序会在屏幕右下角显示蓝牙悬浮窗
3. 点击"开启"按钮开关蓝牙
4. 在设备列表中点击"连接"/"断开"管理设备
5. 右键系统托盘图标可设置开机自启

## 编译

### 前置要求
- .NET 8.0 SDK
- Windows 10 或更高版本

### 步骤

```bash
git clone https://github.com/zk26/KmaoLink.git
cd KmaoLink
dotnet publish -c Release -r win-x64 --self-contained false
```

产物路径：`bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/KmaoLink.exe`

## 兼容性

### Windows 10/11
使用 Windows 原生蓝牙 API，功能完整。

### Windows 7/8
需要下载 `bluetoothCL.exe` 放在程序同目录。
下载地址：https://www.nirsoft.net/utils/bluetooth_command_line.html

## 配置文件

设置保存在 `%AppData%/KmaoLink/settings.json`

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
3. 如果遇到权限问题，右键 exe → 属性 → 勾选"以管理员身份运行"

## 项目结构

```
KmaoLink/
├── .github/workflows/build.yml   # CI/CD 自动编译发布
├── KmaoLink.csproj               # 项目配置
├── Program.cs                    # 入口文件
├── MainForm.cs                   # 主窗体
├── BluetoothManager.cs           # 蓝牙管理核心
├── AutoStartManager.cs           # 开机自启管理
├── SettingsManager.cs            # 设置管理
├── Theme.cs                      # 主题样式
├── logo.png                      # 应用图标
└── logo.ico                      # 图标（exe 用）
```

## 开源协议

MIT License
