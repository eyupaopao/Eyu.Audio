using Eyu.Audio.Provider;
using Eyu.Audio.Utils;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Silk.NET.SDL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Eyu.Audio;
public class SDLOut : IWavePlayer
{
    public SDLOut(AudioDevice? device = null)
    {
        if (device != null && device.IsCapture)
        {
            throw new SdlException(SdlApi.ErrorDeviceTyep);
        }
        if (device == null)
        {
            device = DeviceEnumerator.Instance.RenderDevice.FirstOrDefault();
        }
        if (device == null)
        {
            throw new SdlException(SdlApi.NoOutputDevice);
        }
        this.CurrentDevice = device;
        SdlApi.RenderDeviceChanged += this.SdlApi_RenderDeviceChanged;
    }

    private void SdlApi_RenderDeviceChanged()
    {
        if (CurrentDevice == null)
        {
            CurrentDevice = DeviceEnumerator.Instance.RenderDevice.FirstOrDefault();
        }
        else if (SdlApi.OutPutDevices.Any(e => e.Name == CurrentDevice.Name))
        {
            return;
        }
        else
        {
            CurrentDevice = DeviceEnumerator.Instance.RenderDevice.FirstOrDefault();
        }
        if (CurrentDevice == null)
        {
            SdlApi.Api.CloseAudioDevice(_device);
            PlaybackState = PlaybackState.Stopped;
            PlaybackStopped?.Invoke(this, new StoppedEventArgs(new SdlException(SdlApi.NoOutputDevice)));
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
    private uint _device;

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
        SdlApi.Api.CloseAudioDevice(_device);
    }


    public unsafe void Init(IWaveProvider inputProvider)
    {

        if (PlaybackState != 0)
        {
            throw new InvalidOperationException("Can't re-initialize during playback");
        }
        this.inputProvider = inputProvider;


        var audioSpec = new AudioSpec {
            Freq = inputProvider.WaveFormat.SampleRate,
            Channels = (byte)inputProvider.WaveFormat.Channels,
            Callback = new(AudioCallback),
        };
        AudioSpec suportSpec;
        _device = SdlApi.Api.OpenAudioDevice(CurrentDevice == null ? null : CurrentDevice.Name, 0, &audioSpec, &suportSpec, (int)Sdl.AudioAllowAnyChange);

        if (_device == 0)
        {
            PlaybackStopped?.Invoke(this, new StoppedEventArgs(SdlApi.Api.GetErrorAsException()));
            return;
        }
        var bitsPerSample = suportSpec.Size / suportSpec.Samples / suportSpec.Channels * 8;

        sampleChannel = new SampleChannel(inputProvider, true);
        OutputWaveFormat = new WaveFormat(suportSpec.Freq, (int)bitsPerSample, suportSpec.Channels);
        CreateWaveProvider(sampleChannel, OutputWaveFormat);
        _data = new byte[suportSpec.Size];
    }

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

    // 音频处理回调函数
    unsafe void AudioCallback(void* userdata, byte* stream, int len)
    {
        var readed = outputProvider.Read(_data, 0, len);
        len = Math.Min(len, readed);
        Marshal.Copy(_data, 0, new(stream), len);
    }
    public void Pause()
    {
        SdlApi.Api.PauseAudioDevice(_device, 1);
        PlaybackState = PlaybackState.Paused;
    }

    public void Play()
    {
        SdlApi.Api.PauseAudioDevice(_device, 0);
        PlaybackState = PlaybackState.Playing;
    }

    public void Stop()
    {
        SdlApi.Api.CloseAudioDevice(_device);
        PlaybackState = PlaybackState.Stopped;
        PlaybackStopped?.Invoke(this, new StoppedEventArgs(null));
    }
}

public class SDLDevice
{
    public string Name;
    public int Index;
    public int Capture;

    public SDLDevice(string name, int index, int capture)
    {
        Name = name;
        Index = index;
        Capture = capture;
    }

    public override string? ToString()
    {
        return $"{{name:{Name},index:{Index},capture:{Capture}}}";
    }
}

