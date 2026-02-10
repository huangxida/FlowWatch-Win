# FlowWatch

透明悬浮的网速监控工具，启动后默认驻留系统托盘，在桌面显示实时网速卡片，可锁定置顶或解锁拖动，并支持刷新频率、布局与样式调整。

基于 C# WPF (.NET Framework 4.8) 构建，产物体积 ~5-10MB，内存占用 <50MB。

## 功能
- 托盘驻留：右键菜单提供"设置""固定桌面""置顶""退出"。
- 悬浮窗：实时显示上/下行网速，记忆窗口位置；锁定时可穿透点击，解锁后可拖动。
- 桌面固定：将悬浮窗挂在桌面层级，显示桌面时仍可见，与置顶互斥。
- 布局切换：横向/纵向两种排布，方便放置在屏幕边角。
- 显示模式：速率 / 流量 / 速率+流量 三种模式可切换。
- 刷新频率：1–10 秒可调（默认 1 秒）。
- 配色/字体：网速颜色渐变上限可调（默认 100 Mbps），支持自定义字体与字号（11–19px）。
- 网卡选择：自动选择活跃网卡（优先以太网/Wi-Fi）。
- 开机自启：默认开启，可在设置中关闭。
- 单实例运行：避免重复启动。

## 构建

需要 Visual Studio 2019+ 或 MSBuild + NuGet CLI。

```bash
cd FlowWatch.Windows
nuget restore FlowWatch.sln
msbuild FlowWatch.sln /p:Configuration=Release /p:Platform=x64
```

或使用构建脚本：
```powershell
cd FlowWatch.Windows
.\build.ps1 -Version 1.0.0
```

构建产物位于 `FlowWatch.Windows\FlowWatch\bin\x64\Release\`。

## 目录结构
```
FlowWatch.Windows/
├── FlowWatch.sln
├── FlowWatch/
│   ├── FlowWatch.csproj
│   ├── App.xaml / App.xaml.cs       # 入口、托盘、单实例
│   ├── Services/                    # 网络监控、设置持久化、自启、桌面固定
│   ├── ViewModels/                  # MVVM 数据绑定
│   ├── Views/                       # 悬浮窗、设置窗口
│   ├── Models/                      # 数据模型
│   ├── Helpers/                     # Win32 互操作、格式化、颜色渐变
│   └── Resources/                   # 图标、XAML 样式
├── build.ps1
└── build.bat
assets/
└── icon.png                         # 源图标 (256x256)
```

## 设置项
- 刷新频率：1–10 秒，默认 1 秒
- 锁定悬浮窗置顶：保持置顶并可穿透点击，解锁后可拖动
- 固定在桌面：挂在桌面层级，与置顶互斥
- 开机自启：默认开启，可关闭
- 布局：横向/纵向
- 显示模式：速率 / 流量 / 速率+流量
- 字体：自定义字体族
- 字号：11–19px
- 颜色上限：网速渐变红的阈值（Mbps）
