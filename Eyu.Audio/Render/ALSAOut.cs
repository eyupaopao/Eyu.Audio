using Eyu.Audio.Alsa;
using Eyu.Audio.Provider;
using Eyu.Audio.Utils;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Eyu.Audio;
public class ALSAOut : IWavePlayer
{
    public ALSAOut(AudioDevice? device = null)
    {
        if (device != null && device.IsCapture)
        {
            throw new AlsaDeviceException("Error device type");
        }
        if (device == null)
        {
            device = DeviceEnumerator.Instance.ALSARenderDevices.FirstOrDefault(d => d.DriverType == DriverType.Alsa);
        }
        if (device == null)
        {
            throw new AlsaDeviceException("No output device");
        }
        if (device.DriverType != DriverType.Alsa)
        {
            throw new AlsaDeviceException(SdlApi.ErrorDeviceTyep);
        }
        this.CurrentDevice = device;
        DeviceEnumerator.Instance.RenderDeviceChangedAction += AlsaApi_RenderDeviceChanged;
    }

    /// <summary>
    /// 改变播放硬件
    /// </summary>
    private void AlsaApi_RenderDeviceChanged()
    {
        if (CurrentDevice == null)
        {
            CurrentDevice = DeviceEnumerator.Instance.ALSARenderDevices.FirstOrDefault(d => d.DriverType == DriverType.Alsa);
        }
        else if (DeviceEnumerator.Instance.ALSARenderDevices.Any(e => e.Device == CurrentDevice.Device && e.DriverType == DriverType.Alsa))
        {
            return;
        }
        else
        {
            CurrentDevice = DeviceEnumerator.Instance.ALSARenderDevices.FirstOrDefault(d => d.DriverType == DriverType.Alsa);
        }
        if (CurrentDevice == null)
        {
            ClosePlaybackPcm();
            PlaybackState = PlaybackState.Stopped;
            PlaybackStopped?.Invoke(this, new StoppedEventArgs(new AlsaDeviceException("No output device")));
            return;
        }
        Stop();
        Init(inputProvider);
        Play();
    }

    public float Volume
    {
        get
        {
            return sampleChannel.Volume;
        }
        set
        {
            sampleChannel.Volume = value;
        }
    }

    public AudioDevice? CurrentDevice;
    private IWaveProvider outputProvider;
    private IWaveProvider inputProvider;
    SampleChannel sampleChannel;
    byte[] _data;
    private nint _pcm;
    private nint _params;
    private int _dir;
    private CancellationTokenSource _cancellationTokenSource;
    private Task? _playbackTask;
    private readonly object _lockObject = new object();

    public PlaybackState PlaybackState
    {
        get; private set;
    }
    public WaveFormat OutputWaveFormat
    {
        get; private set;
    }

    public event EventHandler<StoppedEventArgs> PlaybackStopped;

    public void Dispose()
    {
        ClosePlaybackPcm();
        _cancellationTokenSource?.Dispose();
    }

    /// <summary>
    /// 初始化播放器
    /// </summary>
    /// <param name="inputProvider"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public unsafe void Init(IWaveProvider inputProvider)
    {
        if (PlaybackState != 0)
        {
            throw new InvalidOperationException("Can't re-initialize during playback");
        }
        this.inputProvider = inputProvider;

        var deviceName = CurrentDevice?.Device ?? "default";
        var header = WavHeader.Build((uint)inputProvider.WaveFormat.SampleRate, 
            (ushort)inputProvider.WaveFormat.Channels, 
            (ushort)inputProvider.WaveFormat.BitsPerSample);

        OpenPlaybackPcm(deviceName);
        PcmInitialize(_pcm, header, ref _params, ref _dir);

        var bitsPerSample = header.BitsPerSample;
        sampleChannel = new SampleChannel(inputProvider, true);
        OutputWaveFormat = new WaveFormat((int)header.SampleRate, (int)bitsPerSample, header.NumChannels);
        CreateWaveProvider(sampleChannel, OutputWaveFormat);

        unsafe
        {
            ulong frames;
            fixed (int* dirP = &_dir)
                ThrowErrorMessage(InteropAlsa.snd_pcm_hw_params_get_period_size(_params, &frames, dirP), ExceptionMessages.CanNotGetPeriodSize);
            var bufferSize = frames * header.BlockAlign;
            _data = new byte[(int)bufferSize];
        }
    }

    /// <summary>
    /// 构建数据供应器
    /// </summary>
    /// <param name="sampleChannel"></param>
    /// <param name="waveFormat"></param>
    private void CreateWaveProvider(SampleChannel sampleChannel, WaveFormat waveFormat)
    {
        ISampleProvider _sampleProvider = sampleChannel;
        if (waveFormat.SampleRate != this.sampleChannel.WaveFormat.SampleRate)
        {
            _sampleProvider = new SampleWaveFormatConversionProvider(waveFormat, _sampleProvider);
        }
        if (waveFormat.Channels == 1)
        {
            _sampleProvider = new StereoToMonoSampleProvider(_sampleProvider);
        }

        if (waveFormat.BitsPerSample == 32)
            outputProvider = new SampleToWaveProvider(_sampleProvider);
        else if (waveFormat.BitsPerSample == 24)
            outputProvider = new SampleToWaveProvider24(_sampleProvider);
        else
            outputProvider = new SampleToWaveProvider16(_sampleProvider);
    }

    /// <summary>
    /// 播放数据写入循环
    /// </summary>
    private unsafe void WriteStreamLoop()
    {
        PlaybackState = PlaybackState.Playing;
        ulong frames;

        fixed (int* dirP = &_dir)
            ThrowErrorMessage(InteropAlsa.snd_pcm_hw_params_get_period_size(_params, &frames, dirP), ExceptionMessages.CanNotGetPeriodSize);

        var blockAlign = OutputWaveFormat.BlockAlign;
        var bufferSize = frames * (ulong)blockAlign;
        var readBuffer = new byte[(int)bufferSize];
        IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize);

        try
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                if (PlaybackState == PlaybackState.Stopped)
                {
                    break;
                }
                else if (PlaybackState == PlaybackState.Paused)
                {
                    Thread.Sleep(10);
                    continue;
                }

                int read = outputProvider.Read(readBuffer, 0, readBuffer.Length);
                if (read == 0)
                {
                    Thread.Sleep(10);
                    continue;
                }

                // 只复制实际读取的数据
                Marshal.Copy(readBuffer, 0, buffer, read);

                var framesToWrite = (ulong)(read / blockAlign);
                var result = InteropAlsa.snd_pcm_writei(_pcm, buffer, framesToWrite);
                
                if (result < 0)
                {
                    // 尝试恢复错误
                    result = InteropAlsa.snd_pcm_recover(_pcm, (int)result, 0);
                    if (result < 0)
                    {
                        ThrowErrorMessage((int)result, ExceptionMessages.CanNotWriteToDevice);
                        break;
                    }
                }
                else if (result != (long)framesToWrite)
                {
                    // 部分写入，可能需要等待
                    Thread.Sleep(1);
                }
            }
            PlaybackState = PlaybackState.Stopped;
        }
        catch (Exception ex)
        {
            PlaybackState = PlaybackState.Stopped;
            PlaybackStopped?.Invoke(this, new StoppedEventArgs(ex));
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    unsafe void PcmInitialize(nint pcm, WavHeader header, ref nint @params, ref int dir)
    {
        ThrowErrorMessage(InteropAlsa.snd_pcm_hw_params_malloc(ref @params), ExceptionMessages.CanNotAllocateParameters);
        ThrowErrorMessage(InteropAlsa.snd_pcm_hw_params_any(pcm, @params), ExceptionMessages.CanNotFillParameters);
        ThrowErrorMessage(InteropAlsa.snd_pcm_hw_params_set_access(pcm, @params, snd_pcm_access_t.SND_PCM_ACCESS_RW_INTERLEAVED), ExceptionMessages.CanNotSetAccessMode);

        var formatResult = (header.BitsPerSample / 8) switch
        {
            1 => InteropAlsa.snd_pcm_hw_params_set_format(pcm, @params, snd_pcm_format_t.SND_PCM_FORMAT_U8),
            2 => InteropAlsa.snd_pcm_hw_params_set_format(pcm, @params, snd_pcm_format_t.SND_PCM_FORMAT_S16_LE),
            3 => InteropAlsa.snd_pcm_hw_params_set_format(pcm, @params, snd_pcm_format_t.SND_PCM_FORMAT_S24_LE),
            4 => InteropAlsa.snd_pcm_hw_params_set_format(pcm, @params, snd_pcm_format_t.SND_PCM_FORMAT_S32_LE),
            _ => throw new AlsaDeviceException(ExceptionMessages.BitsPerSampleError)
        };
        ThrowErrorMessage(formatResult, ExceptionMessages.CanNotSetFormat);

        ThrowErrorMessage(InteropAlsa.snd_pcm_hw_params_set_channels(pcm, @params, header.NumChannels), ExceptionMessages.CanNotSetChannel);

        var val = header.SampleRate;
        fixed (int* dirP = &dir)
            ThrowErrorMessage(InteropAlsa.snd_pcm_hw_params_set_rate_near(pcm, @params, &val, dirP), ExceptionMessages.CanNotSetRate);

        ThrowErrorMessage(InteropAlsa.snd_pcm_hw_params(pcm, @params), ExceptionMessages.CanNotSetHwParams);
    }

    void OpenPlaybackPcm(string deviceName)
    {
        if (_pcm != default)
            return;

        lock (_lockObject)
        {
            ThrowErrorMessage(InteropAlsa.snd_pcm_open(ref _pcm, deviceName, snd_pcm_stream_t.SND_PCM_STREAM_PLAYBACK, 0), ExceptionMessages.CanNotOpenPlayback);
        }
    }

    void ClosePlaybackPcm()
    {
        if (_pcm == default)
            return;

        lock (_lockObject)
        {
            if (_params != default)
            {
                Marshal.FreeHGlobal(_params);
                _params = default;
            }
            ThrowErrorMessage(InteropAlsa.snd_pcm_close(_pcm), ExceptionMessages.CanNotCloseDevice);
            _pcm = default;
        }
    }

    public void Pause()
    {
        if (PlaybackState != PlaybackState.Playing)
            return;
        
        InteropAlsa.snd_pcm_pause(_pcm, 1);
        PlaybackState = PlaybackState.Paused;
    }

    public void Play()
    {
        if (PlaybackState == PlaybackState.Playing)
            return;

        if (_pcm == default)
        {
            throw new InvalidOperationException("Device not initialized. Call Init() first.");
        }

        if (PlaybackState == PlaybackState.Paused)
        {
            var result = InteropAlsa.snd_pcm_resume(_pcm);
            if (result < 0)
            {
                // 如果恢复失败，可能需要重新启动
                InteropAlsa.snd_pcm_start(_pcm);
            }
            PlaybackState = PlaybackState.Playing;
        }
        else
        {
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            _playbackTask = Task.Run(() => WriteStreamLoop(), _cancellationTokenSource.Token);
            InteropAlsa.snd_pcm_start(_pcm);
        }
    }

    public void Stop()
    {
        if (PlaybackState == PlaybackState.Stopped)
            return;

        PlaybackState = PlaybackState.Stopped;
        _cancellationTokenSource?.Cancel();
        try
        {
            _playbackTask?.Wait(1000);
        }
        catch
        {
            // 忽略等待超时
        }
        
        ClosePlaybackPcm();
        PlaybackStopped?.Invoke(this, new StoppedEventArgs(null));
    }

    void ThrowErrorMessage(int errorNum, string message)
    {
        if (errorNum >= 0)
            return;

        var errorMsg = Marshal.PtrToStringAnsi(InteropAlsa.snd_strerror(errorNum));
        throw new AlsaDeviceException($"{message}. Error {errorNum}. {errorMsg}.");
    }
}
