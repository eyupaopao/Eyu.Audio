using NAudio.Wave;
using Silk.NET.Core.Native;
using Silk.NET.SDL;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Eyu.Audio;
public class SDLOut : IWavePlayer
{
    public float Volume
    {
        get;

        set;
    }
    Sdl _sdl;
    private IWaveProvider _provider;
    byte[] _data;
    private uint _device;
    ushort bufferSize = 4096;

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
        _sdl?.Dispose();
    }

    public unsafe void Init(IWaveProvider waveProvider)
    {
        if (PlaybackState != 0)
        {
            throw new InvalidOperationException("Can't re-initialize during playback");
        }
        _provider = waveProvider;
        OutputWaveFormat = waveProvider.WaveFormat;
        _sdl = Sdl.GetApi();
        if(_sdl == null)
        {
            throw new Exception("open Sdl Faile");
        }
        if (_sdl.InitSubSystem(Sdl.InitAudio) != 0)
        {
            throw _sdl.GetErrorAsException();
        }
        var bytesPerSample = OutputWaveFormat.BitsPerSample / 8;

        ushort formatResult = (OutputWaveFormat.BitsPerSample / 8) switch {
            1 => Sdl.AudioU8,
            2 => Sdl.AudioS16Sys,
            4 => OutputWaveFormat.Encoding switch {
                WaveFormatEncoding.Pcm => Sdl.AudioS32Lsb,
                WaveFormatEncoding.IeeeFloat => Sdl.AudioF32,
                _ => throw new Exception("no support audio format"),
            },
            _ => throw new Exception("no support audio format"),
        };


        var audioSpec = new AudioSpec {
            Freq = OutputWaveFormat.SampleRate,
            Channels = (byte)OutputWaveFormat.Channels,
            Format = formatResult,
            Samples = bufferSize,
            Callback = new(audio_callback),

        };
        _data = new byte[bufferSize];

        _device = _sdl.OpenAudioDevice((string)null, 0, &audioSpec, null, (int)Sdl.AudioAllowAnyChange);
        //var device = _sdl.OpenAudio(&audioSpec, null);
        if (_device == 0)
        {
            throw _sdl.GetErrorAsException();
        }
    }

    // 音频处理回调函数
    unsafe void audio_callback(void* userdata, byte* stream, int len)
    {
        // 从用户数据中获取音频数据
        var readed = _provider.Read(_data, 0, len);
        len = Math.Min(len, readed);
        Marshal.Copy(_data, 0, new(stream), len);
        // 将音频数据写入到音频缓冲区中
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
