# Eyu.Audio

Eyu.Audio 是一个跨平台的音频处理工具包，提供音频录制、播放、格式转换、网络流传输等功能。该库支持多种音频接口和协议，包括 ALSA、SDL、AES67 和 PTP 时间同步。

## 功能特性

- **跨平台支持**: 支持 Windows、Linux、macOS 等操作系统
- **多种音频接口**: 支持 ALSA、SDL 等音频接口
- **音频录制**: 提供音频录制功能，包括系统播放环回采集
- **音频播放**: 支持高质量音频播放
- **音频格式转换**: 支持多种音频格式之间的转换
- **AES67 协议**: 实现 AES67 标准，支持专业音频网络传输
- **PTP 时间同步**: 集成精确时间协议(PTP)，实现音频设备间的时间同步
- **MP3 处理**: 支持 MP3 文件的读取和处理

## 安装

通过 NuGet 包管理器安装：

```
Install-Package Eyu.Audio
```

或使用 .NET CLI：

```
dotnet add package Eyu.Audio
```

## 依赖项

本项目依赖以下 NuGet 包：
- NAudio
- NAudio.Lame.CrossPlatform
- NAudio.Vorbis.Latest
- BunLabs.NAudio.Flac
- NLayer.NAudioSupport
- NWaves
- Silk.NET.SDL
- NullFX.CRC

## 使用示例

### 音频播放

```csharp
using Eyu.Audio.Render;

var audioOut = new SDLOut();
// 初始化并播放音频
```

### 音频录制

```csharp
using Eyu.Audio.Recorder;

var recorder = new SDLCapture();
recorder.StartRecording();
// 处理录制数据
```

### AES67 流传输

```csharp
using Eyu.Audio.AES67;

var channelManager = new Aes67ChannelManager();
var channel = channelManager.CreateMulticastcastChannel("MyChannel");
// 配置并发送音频流
```

### PTP 时间同步

```csharp
using Eyu.Audio.PTP;

var ptpClock = new PTPClock();
ptpClock.Initialize();
ptpClock.Start();
// 实现高精度时间同步
```

## 模块说明

### AES67 模块
- `Aes67Channel`: AES67 通道类，负责音频流的发送和接收
- `Aes67ChannelManager`: AES67 通道管理器，管理多个通道实例
- `RTP`: RTP 数据包构建和转换
- `SDP`: 会话描述协议实现

### ALSA 模块 (Linux)
- `ALSAApi`: ALSA 音频接口封装
- `AlsaDeviceEnumerator`: ALSA 设备枚举器
- `AlsaCapture`: ALSA 录制实现

### PTP 模块
- `PTPClock`: PTP 主从时钟实现
- `PTPClient`: PTP 客户端
- `PTPMessage`: PTP 消息解析
- `PTPTimestamp`: PTP 时间戳处理

### 音频处理模块
- `Provider`: 音频数据提供者，包括格式转换、音量控制等
- `Reader`: 音频文件读取器，支持多种格式
- `Recorder`: 音频录制器，支持多种输入源
- `Render`: 音频播放器，支持多种输出设备
- `Utils`: 各种音频处理工具函数

## Linux 环回采集 (PulseLoopbackCapture) 环境说明

在 Linux 上使用 **PulseLoopbackCapture**（系统播放环回采集）需要系统已安装 PulseAudio 或 PipeWire 相关工具，否则无法采集。

### 必须安装

- **parecord**：用于从默认/指定 monitor 源采集音频。未安装时 `StartRecording()` 会失败并触发 `RecordingStopped`，提示"无法启动 parecord"。

### 各发行版安装示例

| 发行版 | 安装命令 | 说明 |
|--------|----------|------|
| **Ubuntu / Debian** | `sudo apt install pulseaudio-utils` | 传统 PulseAudio |
| **Ubuntu / Debian (PipeWire)** | `sudo apt install pipewire-pulse` | 使用 PipeWire 时，多数桌面版已预装 |
| **Fedora / RHEL** | `sudo dnf install pipewire-pulseaudio` 或 `pulseaudio-utils` | 新版本多用 PipeWire |
| **Arch Linux** | `sudo pacman -S pipewire-pulse` 或 `pulseaudio` | 二选一即可 |
| **openSUSE** | `sudo zypper install pipewire-pulseaudio` 或 `pulseaudio-utils` | 同上 |

### 可选

- **pactl**：一般与 parecord 同包（如 `pulseaudio-utils`）。仅在使用 `GetDefaultMonitorSourceNameFromPactl()` 或需要解析"默认 monitor"设备名时才需要。

### 验证

终端执行：

```bash
parecord --help
```

若有帮助输出，说明环境可用；若提示"未找到命令"，请按上表安装对应包。

## 构建要求

- .NET 8.0 或更高版本
- 对于 Linux 平台，需要相应的音频库（ALSA、PulseAudio/PipeWire）
- 对于 macOS 平台，需要 Core Audio 支持

## 许可证

MIT License

## 开发状态

本项目正在积极开发中，特别是 PTP 时间同步功能仍在完善中（参见 PTP_TODO.md）。

## 贡献

欢迎提交 Issue 和 Pull Request 来改进此项目。