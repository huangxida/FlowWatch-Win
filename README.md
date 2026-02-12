# FlowWatch

A lightweight, transparent overlay network speed monitor for Windows. Displays real-time upload/download speeds on your desktop with per-app traffic tracking and historical statistics.

Built with C# WPF (.NET Framework 4.8), ~5 MB binary, <50 MB memory usage.

**[English](#features) | [中文](#功能)**

---

## Screenshots

<table>
<tr>
<td align="center"><b>Settings (EN)</b></td>
<td align="center"><b>Settings (ZH)</b></td>
</tr>
<tr>
<td><img src="assets/screenshots/settings-general-en.png" width="320"/></td>
<td><img src="assets/screenshots/settings-general-zh.png" width="320"/></td>
</tr>
<tr>
<td><img src="assets/screenshots/settings-appearance-en.png" width="320"/></td>
<td><img src="assets/screenshots/settings-appearance-zh.png" width="320"/></td>
</tr>
</table>

<table>
<tr>
<td align="center"><b>Traffic Statistics</b></td>
<td align="center"><b>App Traffic (Realtime)</b></td>
<td align="center"><b>App Traffic (Daily)</b></td>
</tr>
<tr>
<td><img src="assets/screenshots/statistics-en.png" width="280"/></td>
<td><img src="assets/screenshots/app-traffic-realtime-en.png" width="280"/></td>
<td><img src="assets/screenshots/app-traffic-daily-en.png" width="280"/></td>
</tr>
</table>

## Features

- **Real-time overlay** — Transparent floating window showing upload/download speeds with color-coded gradients
- **Per-app traffic monitoring** — Track network usage by process with ETW (Event Tracing for Windows), view realtime speeds or daily/weekly/monthly totals
- **Traffic statistics** — Daily/weekly/monthly traffic history with bar charts, data retained for 90 days
- **System tray** — Right-click menu for quick access to Settings, Statistics, App Traffic, Pin/Lock, and Exit
- **Lock on Top** — Pin the overlay on top with click-through; unlock to drag and reposition
- **Pin to Desktop** — Attach the overlay to the desktop layer (stays visible on Show Desktop), mutually exclusive with Lock on Top
- **Layout** — Horizontal or vertical arrangement for screen edge placement
- **Display modes** — Speed only, usage only, or speed + usage combined
- **Appearance** — Customizable font family, font size (11–19px), and speed color gradient threshold (1–1000 Mbps)
- **Auto launch** — Starts on login via Windows Task Scheduler (runs elevated)
- **Language** — English, Chinese, or auto-detect from system locale
- **Single instance** — Prevents duplicate launches

## Requirements

- Windows 10 or later (x64)
- **Administrator privileges** required for per-app traffic monitoring (ETW kernel events)

## Installation

Download the latest ZIP from [Releases](../../releases), extract, and run `FlowWatch.exe`.

## Build from Source

Requires Visual Studio 2019+ or MSBuild + NuGet CLI.

```bash
cd FlowWatch.Windows
nuget restore FlowWatch.sln
msbuild FlowWatch.sln /p:Configuration=Release /p:Platform=x64
```

Or use the build script to produce a distributable ZIP:

```powershell
cd FlowWatch.Windows
.\build.ps1 -Version 1.0.0
```

Output: `FlowWatch.Windows\FlowWatch\bin\x64\Release\`

## Project Structure

```
FlowWatch.Windows/
├── FlowWatch.sln
├── build.ps1 / build.bat
└── FlowWatch/
    ├── App.xaml(.cs)              # Entry point, tray icon, single instance
    ├── Services/
    │   ├── NetworkMonitorService  # System-wide speed polling
    │   ├── ProcessTrafficService  # Per-app ETW traffic capture
    │   ├── TrafficHistoryService  # Daily traffic persistence
    │   ├── SettingsService        # JSON settings (atomic write)
    │   ├── LocalizationService    # i18n (EN/ZH dynamic switch)
    │   ├── DesktopPinService      # Pin overlay to desktop layer
    │   └── AutoLaunchService      # Task Scheduler auto-start
    ├── ViewModels/                # MVVM data binding
    ├── Views/
    │   ├── OverlayWindow          # Floating speed display
    │   ├── SettingsWindow         # Configuration UI
    │   ├── StatisticsWindow       # Traffic history charts
    │   └── AppTrafficWindow       # Per-app traffic details
    ├── Models/                    # Data models
    ├── Helpers/                   # Win32 interop, formatting, color gradient
    └── Resources/
        ├── Styles/                # WPF styles
        └── Strings/               # en-US.xaml, zh-CN.xaml
```

## Configuration

Settings are stored at `%LOCALAPPDATA%\FlowWatch\settings.json`.

| Setting | Default | Description |
|---------|---------|-------------|
| Language | auto | `auto` / `en` / `zh` |
| RefreshInterval | 1000 ms | Data polling interval (1–10s) |
| LockOnTop | true | Lock overlay on top with click-through |
| PinToDesktop | false | Pin to desktop layer (exclusive with LockOnTop) |
| AutoLaunch | true | Start on system login |
| Layout | horizontal | `horizontal` / `vertical` |
| DisplayMode | speed | `speed` / `usage` / `both` |
| FontFamily | Segoe UI, Microsoft YaHei | Custom font stack |
| FontSize | 18 | Overlay font size (11–19px) |
| SpeedColorMaxMbps | 100 | White-to-red gradient threshold |

---

# FlowWatch (中文)

轻量级透明悬浮网速监控工具，支持按应用流量监控和历史流量统计。

## 功能

- **实时悬浮窗** — 透明悬浮窗显示实时上传/下载网速，颜色渐变指示速率
- **按应用流量监控** — 基于 ETW 内核事件追踪各进程网络使用，支持实时速度/日/周/月视图
- **流量统计** — 日/周/月流量历史柱状图，数据保留 90 天
- **系统托盘** — 右键菜单：设置、流量统计、应用流量、固定桌面、置顶、退出
- **锁定置顶** — 置顶并穿透点击，解锁后可拖动
- **固定桌面** — 挂在桌面层级（显示桌面时仍可见），与置顶互斥
- **布局** — 横向/纵向两种排布
- **显示模式** — 速率 / 流量 / 速率+流量
- **外观** — 自定义字体、字号（11–19px）、颜色渐变上限（1–1000 Mbps）
- **开机自启** — 通过 Windows 任务计划程序以管理员权限自启
- **语言切换** — 中文、英文、跟随系统，运行时即时切换
- **单实例运行** — 避免重复启动

<table>
<tr>
<td align="center"><b>流量统计</b></td>
<td align="center"><b>应用流量（实时）</b></td>
<td align="center"><b>应用流量（按日）</b></td>
</tr>
<tr>
<td><img src="assets/screenshots/statistics-zh.png" width="280"/></td>
<td><img src="assets/screenshots/app-traffic-realtime-zh.png" width="280"/></td>
<td><img src="assets/screenshots/app-traffic-daily-zh.png" width="280"/></td>
</tr>
</table>

## 系统要求

- Windows 10 或更高版本（x64）
- **需要管理员权限**（ETW 内核事件监控需要）

## 安装

从 [Releases](../../releases) 下载最新 ZIP，解压后运行 `FlowWatch.exe`。

## 构建

```bash
cd FlowWatch.Windows
nuget restore FlowWatch.sln
msbuild FlowWatch.sln /p:Configuration=Release /p:Platform=x64
```
