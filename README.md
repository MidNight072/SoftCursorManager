# 🖱️ SoftCursorManager 光标发白修复

![Release](https://img.shields.io/github/v/release/MidNight072/SoftCursorManager?style=flat-square)
![Platform](https://img.shields.io/badge/platform-Windows%2010%20%2F%2011-blue?style=flat-square)
![Size](https://img.shields.io/badge/exe-10%20KB-success?style=flat-square)
![License](https://img.shields.io/github/license/MidNight072/SoftCursorManager?style=flat-square)

Windows 光标「软件渲染」保活小工具（托盘程序）。

买了自定义鼠标指针却总是**发白 / 失效 / 变回系统默认**？这个 10 KB 的小工具就是为你准备的。⚡

## 🔍 它解决什么问题

Windows 上使用自定义鼠标指针（`.cur` / `.ani`）时，必须依赖**软件渲染**才能生效。
但很多游戏在切换显示模式（尤其全屏独占）时，会把光标渲染重置回**硬件渲染**，
导致自定义指针失效、变回系统默认。

这个工具的核心是**保活**：每 3 秒查询一次当前光标渲染状态
（`SystemParametersInfo(SPI_GETMOUSETRAILS)`），发现被重置为硬件渲染时就立刻重新应用
软件渲染，让自定义指针在游戏里也一直保持。每次自动恢复会写入日志
（`%TEMP%\SoftCursorManager.log`）。

> 附注：知名的 SoftCursor 1.0 采用的是同一个系统调用
> （`SPI_SETMOUSETRAILS`，通过"鼠标轨迹"开关强制软件渲染）。
> 本工具与它使用相同机制，但为独立编写的实现（未使用其任何代码），
> 在此基础上增加了自动保活、托盘管理和开机自启。

## 🌟 为什么选它

- ⚡ **极致轻量** —— 主程序仅约 10 KB，内存占用约 30 MB（WinForms 托盘程序基线），CPU 占用接近 0
- 🎮 **打游戏无感** —— 没有 hook、没有注入、没有驱动，只是每 3 秒一次纯系统调用，不碰游戏进程
- 🖥️ **指针方案一键切换** —— 托盘菜单列出系统所有指针方案，点击即换
- 📥 **一键导入皮肤** —— 指针文件夹直接拖进来就能装，不用右键 install.inf
- 🚫 **无广告 / 无联网 / 无后台服务** —— 单文件，退出即走
- 🔁 **开机自启可选** —— 想让它常驻就勾上，不想就关

## 📊 资源占用

| 项目 | 数值 |
| --- | --- |
| 主程序体积 | ≈ 10 KB |
| 内存占用 | ≈ 30 MB（托盘常驻的普通水平） |
| CPU 占用 | ≈ 0%（每 3 秒一次系统调用） |
| 磁盘写入 | 仅日志（每次自动恢复才追加一行） |

## 🎮 打游戏须知

- 工具本身对游戏性能**没有可感知的影响**（不 hook、不注入、不读游戏内存）
- 软件渲染模式下，如果**游戏本身掉帧**，光标会跟着画面略卡 —— 这是软件渲染的特性，不是本工具造成的；此时可临时切回硬件光标
- 游戏切换显示模式重置光标后，**约 3 秒内自动恢复**，不用再手动开软光标工具

## 🌟 功能（托盘右键菜单）

| 菜单项 | 作用 |
| --- | --- |
| 软件光标（保活中） | 切换到软件渲染并持续保活（推荐） |
| 硬件光标（暂停保活） | 切回硬件渲染，同时暂停轮询 |
| 指针方案 ▸ | 列出系统所有已注册的指针方案（系统自带 + 你安装的），点击即切换，当前方案打勾 |
| 导入指针皮肤... | 选择一个指针文件夹（.cur / .ani，可含 install.inf），自动安装为新方案并立即应用；没有 inf 时按文件名智能识别 |
| 开机自启 | 勾选后随系统启动自动运行 |
| 打开日志 | 用记事本查看每次自动恢复记录 |
| 退出 | 关闭程序（自启设置保留） |

当前渲染模式在菜单里有勾选标记；左键单击托盘图标也可以刷新状态。

### 📥 导入皮肤说明

- 有 `install.inf` 的完整指针包：自动按 inf 里的映射安装（最准确）
- 只有光标文件的裸文件夹：按常见命名识别（Normal/Arrow → 箭头、Busy/Wait → 等待、Link → 手型 等），识别不出的槽位回退到系统默认
- 导入的皮肤复制到 `%LOCALAPPDATA%\Cursors\<方案名>\`，原文件夹可以随意移动或删除
- 导入后自动应用，之后可在「指针方案」菜单里随时切换

## 📦 用法

1. 从 [Releases](https://github.com/MidNight072/SoftCursorManager/releases) 下载压缩包并解压
2. 双击运行 `SoftCursorManager.exe`（不需要安装，托盘出现小箭头图标）
3. 右键菜单勾选「开机自启」即可

🧹 卸载：运行 `卸载.bat`，会自动停止进程、删除自启项、恢复默认光标方案，然后可以直接删除整个文件夹。

## 🛠️ 构建

需要 Windows 自带的 .NET Framework：

```bat
build.bat
```

输出 `SoftCursorManager.exe`（约 10 KB）。

或手动：

```bat
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /out:SoftCursorManager.exe /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll SoftCursorManager.cs
```

运行环境：Windows 10 / 11（.NET Framework 4.x 内置）。

## 🔬 技术说明

- 渲染状态检测：`SPI_GETMOUSETRAILS`（0x5E）—— `-1` 为软件渲染，`0/1` 为硬件渲染
- 渲染切换：`SPI_SETMOUSETRAILS`（0x5D）
- 保活间隔：3 秒（轮询，纯系统调用，几乎零资源占用）
- 单实例互斥，防止重复运行
- 自启：写 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`

## ⚠️ 免责声明

- 本仓库**不包含任何鼠标指针素材**。指针皮肤属于对应作者/卖家的版权作品，请自行购买或获取。
- 软件渲染模式下，游戏画面卡顿时光标会跟着略卡，这是软件渲染的正常特性；此时可临时切换回硬件渲染。

## 📄 License

本项目基于 [MIT License](LICENSE) 开源。
