using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace Eyu.Audio;

/// <summary>
/// Linux 环回采集：通过 PulseAudio/PipeWire 的 monitor 源采集系统播放的音频。
/// 使用 parecord 子进程实现，兼容 PulseAudio 与 PipeWire，避免 pa_simple 的 "Bad state" 问题。
/// 接口为 NAudio 的 IWaveIn。
/// </summary>
/// <remarks>
/// <para><b>环境要求（必须安装）：</b></para>
/// <list type="bullet">
/// <item><b>parecord</b>：用于从 monitor 源采集，通常随以下包提供：</item>
/// <item>• Ubuntu/Debian：<c>pulseaudio-utils</c> 或 <c>pipewire-pulse</c></item>
/// <item>• Fedora/RHEL：<c>pipewire-pulseaudio</c> 或 <c>pulseaudio-utils</c></item>
/// <item>• Arch：<c>pipewire-pulse</c> 或 <c>pulseaudio</c></item>
/// <item>• openSUSE：<c>pipewire-pulseaudio</c> 或 <c>pulseaudio-utils</c></item>
/// <item><b>pactl</b>（可选）：仅当使用 GetDefaultMonitorSourceNameFromPactl() 或需解析设备名时需要，一般与 parecord 同包。</item>
/// </list>
/// <para>若未安装，StartRecording() 会触发 RecordingStopped 并提示“无法启动 parecord”。</para>
/// <para>安装示例（Ubuntu/Debian）：<c>sudo apt install pulseaudio-utils</c></para>
/// <para>使用 PipeWire 时：<c>sudo apt install pipewire-pulse</c>（通常已随桌面安装）。</para>
/// </remarks>
[SupportedOSPlatform("linux")]
public class PulseLoopbackCapture : IWaveIn
{
    private readonly string _sourceName;
    private readonly int _audioBufferMillisecondsLength;
    private Process? _parecordProcess;
    private volatile bool _isRecording;
    private CancellationTokenSource? _cts;
    private Task? _readTask;

    public PulseLoopbackCapture(string? monitorSourceName = null, int audioBufferMillisecondsLength = 100)
    {
        _sourceName = monitorSourceName ?? "@DEFAULT_MONITOR@";
        _audioBufferMillisecondsLength = audioBufferMillisecondsLength;
        WaveFormat = new WaveFormat(48000, 16, 2);
    }

    public WaveFormat WaveFormat { get; set; }

    public event EventHandler<WaveInEventArgs>? DataAvailable;
    public event EventHandler<StoppedEventArgs>? RecordingStopped;

    /// <summary>
    /// 获取默认 monitor 源名称。优先返回 @DEFAULT_MONITOR@（Pulse/PipeWire 内置），
    /// 若需具体设备名可调用 pactl get-default-sink 并加 .monitor。
    /// </summary>
    public static string? GetDefaultMonitorSourceName()
    {
        return "@DEFAULT_MONITOR@";
    }

    /// <summary>
    /// 通过 pactl 解析得到默认 sink 的 monitor 名称（如 alsa_output.xxx.monitor），供需要具体设备名时使用。
    /// </summary>
    public static string? GetDefaultMonitorSourceNameFromPactl()
    {
        try
        {
            string sink = RunCommand("pactl get-default-sink");
            if (string.IsNullOrWhiteSpace(sink))
                return null;
            return sink.Trim() + ".monitor";
        }
        catch
        {
            return null;
        }
    }

    private static string RunCommand(string command)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = "-c \"" + command.Replace("\"", "\\\"") + "\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = new Process { StartInfo = startInfo };
        process.Start();
        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit(2000);
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"pactl failed: {output}");
        return output;
    }

    public void StartRecording()
    {
        if (_isRecording)
            return;

        var wf = WaveFormat;
        string format = "s16le";
        if (wf.BitsPerSample == 32 && wf.Encoding == WaveFormatEncoding.IeeeFloat)
            format = "float32le";
        else if (wf.BitsPerSample == 24)
            format = "s24le";

        string deviceArg = string.IsNullOrEmpty(_sourceName) ? "@DEFAULT_MONITOR@" : _sourceName;
        string args = $"-d \"{deviceArg}\" --raw --rate={wf.SampleRate} --channels={wf.Channels} --format={format}";

        var startInfo = new ProcessStartInfo
        {
            FileName = "parecord",
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            _parecordProcess = new Process { StartInfo = startInfo };
            _parecordProcess.Start();
        }
        catch (Exception ex)
        {
            RecordingStopped?.Invoke(this, new StoppedEventArgs(new InvalidOperationException($"无法启动 parecord，请安装 pulseaudio-utils 或 pipewire-pulse: {ex.Message}", ex)));
            return;
        }

        if (_parecordProcess.HasExited)
        {
            string err = _parecordProcess.StandardError?.ReadToEnd() ?? "";
            RecordingStopped?.Invoke(this, new StoppedEventArgs(new InvalidOperationException($"parecord 立即退出: {err}")));
            _parecordProcess = null;
            return;
        }

        _isRecording = true;
        _cts = new CancellationTokenSource();
        // 读取更频繁：小块读取，减少延迟累积
        int readChunkSize = 512;
        int outputChunkBytes = Math.Max(1024, (int)((_audioBufferMillisecondsLength / 1000.0) * wf.AverageBytesPerSecond));
        var bufferedProvider = new BufferedWaveProvider(wf) { DiscardOnBufferOverflow = true };
        Stream stdout = _parecordProcess.StandardOutput.BaseStream;

        _readTask = Task.Run(() =>
        {
            byte[] readBuffer = new byte[readChunkSize];
            byte[] outputBuffer = new byte[outputChunkBytes];
            try
            {
                while (_isRecording && _parecordProcess != null && !_parecordProcess.HasExited && !_cts!.Token.IsCancellationRequested)
                {
                    int read = stdout.Read(readBuffer, 0, readBuffer.Length);
                    if (read <= 0)
                        break;
                    bufferedProvider.AddSamples(readBuffer, 0, read);
                    // 攒够再回调，减少网络发包频率，缓解卡顿
                    while (bufferedProvider.BufferedBytes >= outputChunkBytes)
                    {
                        int got = bufferedProvider.Read(outputBuffer, 0, outputChunkBytes);
                        if (got > 0)
                            DataAvailable?.Invoke(this, new WaveInEventArgs(outputBuffer, got));
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
            catch (Exception ex)
            {
                if (_isRecording)
                    RecordingStopped?.Invoke(this, new StoppedEventArgs(ex));
            }
            finally
            {
                if (_isRecording)
                {
                    _isRecording = false;
                    RecordingStopped?.Invoke(this, new StoppedEventArgs());
                }
            }
        }, _cts.Token);
    }

    public void StopRecording()
    {
        if (!_isRecording)
            return;
        _isRecording = false;
        _cts?.Cancel();
        try
        {
            _parecordProcess?.Kill();
            _readTask?.Wait(1500);
        }
        catch { }
        try
        {
            _parecordProcess?.Dispose();
        }
        catch { }
        _parecordProcess = null;
        _cts?.Dispose();
        _cts = null;
        RecordingStopped?.Invoke(this, new StoppedEventArgs());
    }

    public void Dispose()
    {
        StopRecording();
        GC.SuppressFinalize(this);
    }
}
