# DesktopSplit 发布产物

项目提供两种 Windows x64 发布方式，均为自包含版本，不要求目标电脑预先安装 .NET 运行时。

## 目录版

输出目录：`dist/DesktopSplit-directory/`

这个版本包含 `DesktopSplit.exe` 和它需要的 DLL/运行时文件，适合放在固定安装目录、手工解压目录或交给安装器使用。EXE 文件本身带有 DesktopSplit 图标，托盘图标从程序集内置的同一份 `.ico` 读取。

构建命令：

```powershell
& ".\.tools\dotnet\dotnet.exe" publish -p:PublishProfile=DesktopSplit-directory
```

## 单文件版

输出文件：`dist/DesktopSplit-single/DesktopSplit.exe`

这个版本把程序、.NET 运行时和原生依赖合并为一个自包含 EXE，适合直接复制和运行。首次启动时系统可能需要短暂解压原生组件，程序本身不需要旁边的 DLL。

构建命令：

```powershell
& ".\.tools\dotnet\dotnet.exe" publish -p:PublishProfile=DesktopSplit-single-file
```

两个版本都使用 `Assets/DesktopSplit.ico`；PNG 是源图，ICO 包含 16、24、32、48、64、128、256 像素尺寸。当前没有创建 MSI/Inno Setup 安装器，目录版已经可以作为安装目录交给后续安装器使用。
