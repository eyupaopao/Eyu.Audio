# Eyu.Audio

this is a cross platform audio tools package

## Linux 环回采集 (PulseLoopbackCapture) 环境说明

在 Linux 上使用 **PulseLoopbackCapture**（系统播放环回采集）需要系统已安装 PulseAudio 或 PipeWire 相关工具，否则无法采集。

### 必须安装

- **parecord**：用于从默认/指定 monitor 源采集音频。未安装时 `StartRecording()` 会失败并触发 `RecordingStopped`，提示“无法启动 parecord”。

### 各发行版安装示例

| 发行版 | 安装命令 | 说明 |
|--------|----------|------|
| **Ubuntu / Debian** | `sudo apt install pulseaudio-utils` | 传统 PulseAudio |
| **Ubuntu / Debian (PipeWire)** | `sudo apt install pipewire-pulse` | 使用 PipeWire 时，多数桌面版已预装 |
| **Fedora / RHEL** | `sudo dnf install pipewire-pulseaudio` 或 `pulseaudio-utils` | 新版本多用 PipeWire |
| **Arch Linux** | `sudo pacman -S pipewire-pulse` 或 `pulseaudio` | 二选一即可 |
| **openSUSE** | `sudo zypper install pipewire-pulseaudio` 或 `pulseaudio-utils` | 同上 |

### 可选

- **pactl**：一般与 parecord 同包（如 `pulseaudio-utils`）。仅在使用 `GetDefaultMonitorSourceNameFromPactl()` 或需要解析“默认 monitor”设备名时才需要。

### 验证

终端执行：

```bash
parecord --help
```

若有帮助输出，说明环境可用；若提示“未找到命令”，请按上表安装对应包。
