using Eyu.Audio.Alsa;
using NAudio.Wave;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Eyu.Audio;

/// <summary>
/// ALSA 环回采集：直接通过 ALSA API 采集系统播放的音频（模拟环回捕获）
/// 性能优于通过外部进程（如 parecord）的方式
/// </summary>
public class ALSALoopbackCapture : IWaveIn
{
    private readonly string _deviceName;
    private readonly int _audioBufferMillisecondsLength;
    private ALSAApi? _alsaApi;
    private SoundDeviceSettings _settings;
    private volatile bool _isRecording;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _recordTask;

    public ALSALoopbackCapture(string? deviceName = null, int audioBufferMillisecondsLength = 100)
    {
        _deviceName = deviceName ?? "default";
        _audioBufferMillisecondsLength = audioBufferMillisecondsLength;
        WaveFormat = new WaveFormat(48000, 16, 2); // 默认48kHz, 16位, 立体声
    }

    public WaveFormat WaveFormat { get; set; }

    public event EventHandler<WaveInEventArgs>? DataAvailable;
    public event EventHandler<StoppedEventArgs>? RecordingStopped;

    public void StartRecording()
    {
        if (_isRecording)
            return;

        try
        {
            // 设置录音设备参数
            _settings = new SoundDeviceSettings
            {
                RecordingDeviceName = _deviceName,
                RecordingSampleRate = (ushort)WaveFormat.SampleRate,
                RecordingChannels = (ushort)WaveFormat.Channels,
                RecordingBitsPerSample = (ushort)WaveFormat.BitsPerSample
            };

            // 创建 ALSA API 实例
            _alsaApi = new ALSAApi(_settings);
            
            // 获取实际支持的格式
            WaveFormat actualFormat = _alsaApi.GetFormat(true);
            WaveFormat = actualFormat; // 更新为实际格式

            _isRecording = true;
            _cancellationTokenSource = new CancellationTokenSource();

            // 计算缓冲区大小
            int outputChunkBytes = Math.Max(1024, (int)((_audioBufferMillisecondsLength / 1000.0) * WaveFormat.AverageBytesPerSecond));
            var bufferedProvider = new BufferedWaveProvider(WaveFormat) { DiscardOnBufferOverflow = true };

            // 开始录音任务
            _recordTask = Task.Run(() =>
            {
                try
                {
                    _alsaApi.Loopback(buffer =>
                    {
                        if (!_isRecording || _cancellationTokenSource.Token.IsCancellationRequested)
                            return;

                        // 添加采样到缓冲区
                        bufferedProvider.AddSamples(buffer, 0, buffer.Length);
                        
                        // 当缓冲区达到指定大小时触发事件
                        while (bufferedProvider.BufferedBytes >= outputChunkBytes)
                        {
                            var outputBuffer = new byte[outputChunkBytes];
                            int got = bufferedProvider.Read(outputBuffer, 0, outputChunkBytes);
                            if (got > 0)
                                DataAvailable?.Invoke(this, new WaveInEventArgs(outputBuffer, got));
                        }
                    }, _cancellationTokenSource.Token);
                }
                catch (Exception ex)
                {
                    if (_isRecording)
                    {
                        _isRecording = false;
                        RecordingStopped?.Invoke(this, new StoppedEventArgs(ex));
                    }
                }
                finally
                {
                    if (_isRecording)
                    {
                        _isRecording = false;
                        RecordingStopped?.Invoke(this, new StoppedEventArgs());
                    }
                }
            }, _cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            RecordingStopped?.Invoke(this, new StoppedEventArgs(ex));
        }
    }

    public void StopRecording()
    {
        if (!_isRecording)
            return;

        _isRecording = false;
        _cancellationTokenSource?.Cancel();

        try
        {
            _alsaApi?.Dispose().Wait(100); // 等待最多1秒完成清理
        }
        catch { }

        try
        {
            _recordTask?.Wait(1500); // 等待记录任务结束
        }
        catch { }

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _alsaApi = null;
    }

    public void Dispose()
    {
        StopRecording();
        GC.SuppressFinalize(this);
    }
}