﻿using Eyu.Audio.Provider;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Silk.NET.Core.Native;
using Silk.NET.SDL;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;

namespace Eyu.Audio;
public class SDLOut : IWavePlayer
{
    public float Volume
    {
        get;
        set;
    }
    static Sdl _sdl = Sdl.GetApi();
    static SDLOut()
    {
        _sdl.Init(Sdl.InitAudio | Sdl.InitEvents);
    }
    private IWaveProvider _provider;
    byte[] _data;
    private uint _device;
    string _deviceName;

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
        _sdl?.CloseAudioDevice(_device);
    }


    public static unsafe List<SDLDevice> GetDeviceNames(int capture)
    {
        var list = new List<SDLDevice>();
        var num = _sdl.GetNumAudioDevices(capture);
        if (num > 1)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            for (int i = 0; i < num; i++)
            {
                var name = Encoding.UTF8.GetString(Encoding.GetEncoding("GB2312").GetBytes(_sdl.GetAudioDeviceNameS(i, capture)));
                //IntPtr namePtr = new IntPtr(_sdl.GetAudioDeviceName(i, capture));
                //var name = Marshal.Copy(namePtr);
                list.Add(new SDLDevice(_sdl.GetAudioDeviceNameS(i, capture), i, capture));
            }
        }
        return list;
    }

    public unsafe void Init(IWaveProvider waveProvider)
    {
        if (PlaybackState != 0)
        {
            throw new InvalidOperationException("Can't re-initialize during playback");
        }
        OutputWaveFormat = waveProvider.WaveFormat;
        if (_sdl == null)
        {
            throw new Exception("open Sdl Faile");
        }
        if (_sdl.InitSubSystem(Sdl.InitAudio) != 0)
        {
            throw _sdl.GetErrorAsException();
        }

        var audioSpec = new AudioSpec
        {
            Freq = OutputWaveFormat.SampleRate,
            Callback = new(audio_callback),

        };
        AudioSpec audioSpec1;
        var name = _sdl.GetAudioDeviceName(0, 0);
        _device = _sdl.OpenAudioDevice(name, 0, &audioSpec, &audioSpec1, (int)Sdl.AudioAllowAnyChange);
        if (_device == 0)
        {
            throw _sdl.GetErrorAsException();
        }
        var bitsPerSample = audioSpec1.Size / audioSpec1.Samples / audioSpec1.Channels * 8;

        InitWaveProvider(waveProvider, new WaveFormat(audioSpec1.Freq, (int)bitsPerSample, audioSpec1.Channels));
        _data = new byte[audioSpec1.Size];
        //var device = _sdl.OpenAudio(&audioSpec, null);
    }

    private void InitWaveProvider(IWaveProvider waveProvider, WaveFormat waveFormat)
    {
        ISampleProvider sampleChannel = new SampleChannel(waveProvider, true);
        if (waveFormat.SampleRate != sampleChannel.WaveFormat.SampleRate)
        {
            sampleChannel = new SampleWaveFormatConversionProvider(waveFormat, sampleChannel);
        }
        if (waveFormat.Channels == 1)
        {
            sampleChannel = new StereoToMonoSampleProvider(sampleChannel);
        }

        if (waveFormat.BitsPerSample == 32)
            _provider = new SampleToWaveProvider(sampleChannel);
        else if (waveFormat.BitsPerSample == 24)
            _provider = new SampleToWaveProvider24(sampleChannel);
        else
            _provider = new SampleToWaveProvider16(sampleChannel);

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
        _sdl.PauseAudioDevice(_device, 1);
        PlaybackState = PlaybackState.Paused;
    }

    public void Play()
    {
        _sdl.PauseAudioDevice(_device, 0);
        PlaybackState = PlaybackState.Playing;
    }

    public void Stop()
    {
        _sdl?.CloseAudioDevice(_device);
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

