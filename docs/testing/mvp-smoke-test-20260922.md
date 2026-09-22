# 首版冒烟测试记录（2026-09-22）

## 结果

- `dotnet build -c Debug`：通过，无编译错误、无警告。
- `dotnet publish -c Release -r win-x64 --self-contained false`：通过。
- Debug DLL 启动：通过，进程持续运行，托盘/WPF 初始化未抛异常。
- Release 发布版 EXE 启动：通过，进程响应正常。
- 真实窗口最大化测试：通过。在 `2048x1112` 工作区中，记事本最大化后被管理为左侧约 `1638x1112` 的 4/5 区域。
- 目录版自包含发布：通过，输出到 `dist/DesktopSplit-directory/`，包含 EXE 与运行时依赖。
- 单文件自包含发布：通过，输出目录内只有 `DesktopSplit.exe`。
- 目录版 EXE 启动：通过，进程持续运行，托盘初始化未抛异常。
- 单文件版 EXE 启动：通过，进程持续运行，托盘初始化未抛异常。
- EXE 与托盘图标：已接入同一份 `Assets/DesktopSplit.ico`；EXE 使用应用图标，托盘使用嵌入资源。
- 设置窗口像素编辑：通过，能读取当前工作区像素宽高，按区域数量生成宽度输入框，并成功触发一次“应用像素宽度”。
- 当前用户开机启动项：启动测试后可写入 `HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run\\DesktopSplit`。

## 本轮构建产物

- 目录版：`dist/DesktopSplit-directory/DesktopSplit.exe`
- 单文件版：`dist/DesktopSplit-single/DesktopSplit.exe`

## 尚未完成的人工验证

仍需在真实桌面上手动验证记事本/浏览器的最大化、还原、窗口拖动后按中心点重新归属、DPI 缩放以及托盘暂停/恢复。当前自动化冒烟测试覆盖进程启动、窗口最大化管理和两种发布产物启动，不替代这些交互验证。
