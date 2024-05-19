using Eyu.Audio.Provider;
using Eyu.Audio.Utils;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Silk.NET.SDL;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Eyu.Audio;
public class SDLOut : IWavePlayer
{
    public SDLOut(int deviceIndex = -1)
    {
        this.deviceIndex = deviceIndex;
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
    private IWaveProvider _provider;
    SampleChannel sampleChannel;
    byte[] _data;
    private uint _device;
    private readonly int deviceIndex;

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
        SdlApi.CloseAudioDevice(_device);
    }


    public unsafe void Init(IWaveProvider waveProvider)
    {
        if (PlaybackState != 0)
        {
            throw new InvalidOperationException("Can't re-initialize during playback");
        }
        OutputWaveFormat = waveProvider.WaveFormat;
       
        if (SdlApi.InitAudio() != 0)
        {
            PlaybackStopped?.Invoke(this, new StoppedEventArgs(SdlApi.GetErrorAsException()));
            return;
        }

        var audioSpec = new AudioSpec {
            Freq = OutputWaveFormat.SampleRate,
            Callback = new(audio_callback),

        };
        AudioSpec suportSpec;

        byte* deviceName = null;
        if (deviceIndex > -1)
        {
            deviceName = SdlApi.GetAudioDeviceName(deviceIndex, 0);
        }
        _device = SdlApi.OpenAudioDevice(deviceName, 0, &audioSpec, &suportSpec);
        if (_device == 0)
        {
            PlaybackStopped?.Invoke(this, new StoppedEventArgs(SdlApi.GetErrorAsException()));
            return;
        }
        var bitsPerSample = suportSpec.Size / suportSpec.Samples / suportSpec.Channels * 8;

        sampleChannel = new SampleChannel(waveProvider, true);
        //_provider = sampleChannel;
        InitWaveProvider(sampleChannel, new WaveFormat(suportSpec.Freq, (int)bitsPerSample, suportSpec.Channels));
        _data = new byte[suportSpec.Size];
        //var device = _sdl.OpenAudio(&audioSpec, null);

    }

    private void InitWaveProvider(SampleChannel sampleChannel, WaveFormat waveFormat)
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
            _provider = new SampleToWaveProvider(_sampleProvider);
        else if (waveFormat.BitsPerSample == 24)
            _provider = new SampleToWaveProvider24(_sampleProvider);
        else
            _provider = new SampleToWaveProvider16(_sampleProvider);

    }

    // 音频处理回调函数
    unsafe void audio_callback(void* userdata, byte* stream, int len)
    {
        var readed = _provider.Read(_data, 0, len);
        len = Math.Min(len, readed);
        Marshal.Copy(_data, 0, new(stream), len);
    }
    public void Pause()
    {
        SdlApi.PauseAudioDevice(_device, 1);
        PlaybackState = PlaybackState.Paused;
    }

    public void Play()
    {
        SdlApi.PauseAudioDevice(_device, 0);
        PlaybackState = PlaybackState.Playing;
    }

    public void Stop()
    {
        SdlApi.CloseAudioDevice(_device);
        PlaybackState = PlaybackState.Stopped;
        PlaybackStopped?.Invoke(this, null);
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
}

