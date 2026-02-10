# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

本机 MSBuild 路径：
```
C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\amd64\MSBuild.exe
```

NuGet 还原 + 构建（在 bash/git-bash 中执行）：
```bash
cd FlowWatch.Windows
# NuGet restore（nuget.exe 已在项目根目录）
../nuget.exe restore FlowWatch.sln

# MSBuild 构建（使用 -p 代替 /p 以兼容 bash）
"/c/Program Files (x86)/Microsoft Visual Studio/18/BuildTools/MSBuild/Current/Bin/amd64/MSBuild.exe" FlowWatch.sln -p:Configuration=Release -p:Platform=x64 -v:minimal
```

打包 ZIP：
```powershell
cd FlowWatch.Windows
.\build.ps1 -Version 1.0.0
```

Output: `FlowWatch.Windows\FlowWatch\bin\x64\Release\`

Release via CI: push a `v*` tag → GitHub Actions builds and publishes a ZIP to GitHub Releases.

## Architecture

C# WPF application targeting .NET Framework 4.8 (x64 only). MVVM pattern with singleton services. No DI container — services use `Lazy<T>` singletons accessed via `ServiceType.Instance`.

### Data flow

```
NetworkMonitorService (DispatcherTimer polls GetIPv4Statistics())
    → fires StatsUpdated event
        → OverlayViewModel formats speeds/colors, updates bound properties
            → OverlayWindow.xaml renders via data binding

SettingsService (JSON at %LOCALAPPDATA%\FlowWatch\settings.json)
    → fires SettingsChanged event
        → OverlayViewModel re-applies font/layout/display mode
        → SettingsViewModel syncs UI controls
        → OverlayWindow applies lock/pin/topmost state
        → App updates tray menu checkboxes
```

### Key interactions

- **LockOnTop ↔ PinToDesktop** are mutually exclusive. Setting one clears the other in SettingsService, SettingsViewModel, and tray menu.
- **Click-through**: When locked or pinned, `NativeInterop.SetClickThrough` adds `WS_EX_TRANSPARENT` via `SetWindowLongPtr`.
- **Desktop pin**: `DesktopPinService` uses Progman → `SendMessage(0x052C)` → `EnumWindows` to find WorkerW → `SetParent` to reparent the overlay beneath desktop icons.
- **Settings persistence**: Atomic write (temp file → `File.Replace` → .bak).
- **Single instance**: `Mutex("FlowWatch_SingleInstance")` in `App.OnStartup`.
- **SettingsWindow** hides on close (never destroyed until exit via `ForceClose()`).

### NuGet dependencies

- **Hardcodet.NotifyIcon.Wpf 1.1.0** — system tray icon
- **System.Text.Json 6.0.10** — settings serialization

All other APIs are built-in: `System.Net.NetworkInformation`, `Microsoft.Win32` (registry), Win32 P/Invoke via `NativeInterop`.
