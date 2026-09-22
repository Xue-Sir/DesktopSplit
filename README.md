# DesktopSplit

DesktopSplit 是一个面向 Windows 10 的轻量屏幕分区工具。它不创建虚拟显示器，而是把当前显示器的工作区按比例划分成多个区域；窗口的中心点落在哪个区域，就认为它属于哪个“屏幕”。

项目仓库：[github.com/Xue-Sir/DesktopSplit](https://github.com/Xue-Sir/DesktopSplit)

当前首版实现：

- 托盘驻留和开机自启动；
- 预设布局：左右均分、4/5 + 1/5、三列；
- 两区域预览中可拖动分隔线调比例；
- 设置页显示当前显示器工作区和各区域的实际像素尺寸，可直接输入各列宽度（像素）调整布局；
- 自定义预设保存到 `%AppData%\DesktopSplit\settings.json`；
- 窗口执行原生最大化时，填充中心所在区域；再次最大化/恢复操作可回到原始位置；
- 普通窗口不会被自动吸附或移动；布局改变只立即调整已经由 DesktopSplit 管理的最大化窗口；
- 托盘菜单可暂停/恢复分区行为。

## 本地构建

项目使用 WPF + Win32 API，SDK 版本记录在 `global.json`。如果本机没有对应 SDK，可使用 Microsoft .NET SDK 的 Windows x64 二进制归档解压到项目的 `.tools\dotnet`，然后执行：

```powershell
$env:DOTNET_ROOT = "E:\SomeTry\DesktopSpilt\.tools\dotnet"
& "$env:DOTNET_ROOT\dotnet.exe" build -c Debug
```

## 当前边界

这是第一版可运行骨架，窗口状态检测采用轻量轮询，暂不处理虚拟显示器、自动吸附、全屏程序、UAC 安全桌面或多显示器独立布局。下一步应重点在真实窗口上验证最大化/还原、DPI 缩放、任务栏工作区和启动项行为。

项目说明、决策记录和测试计划统一放在 [`docs/`](docs/README.md) 下；当前没有 Git 提交。

发布产物说明见 [`docs/reference/distribution.md`](docs/reference/distribution.md)，包括目录版和单文件自包含版。应用图标源文件位于 `Assets/DesktopSplitIcon.png`，Windows 多尺寸图标位于 `Assets/DesktopSplit.ico`。
