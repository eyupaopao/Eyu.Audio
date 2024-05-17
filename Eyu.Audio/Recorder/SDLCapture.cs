using NAudio.Wave;
using Silk.NET.SDL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Eyu.Audio.Recorder;

public unsafe class SDLCapture : IWaveIn
{

    public SDLCapture()
    {
        WaveFormat = new WaveFormat(8000, 16, 1);
    }

    private Sdl _sdl;
    private uint _device;
    public WaveFormat WaveFormat
    {
        get;
        set;
    }

    public event EventHandler<WaveInEventArgs> DataAvailable;
    public event EventHandler<StoppedEventArgs> RecordingStopped;

    public void Dispose()
    {
        StopRecording();
    }
    AudioSpec sourceSpec;
    public unsafe void StartRecording()
    {
        _sdl = Sdl.GetApi();
        if (_sdl == null)
        {
            RecordingStopped?.Invoke(this, new StoppedEventArgs(new Exception("open Sdl Faile")));
            return;
        }
        if (_sdl.InitSubSystem(Sdl.InitAudio) != 0)
        {
            RecordingStopped?.Invoke(this, new StoppedEventArgs(_sdl.GetErrorAsException()));
            return;
        }
        var audioSpec = new AudioSpec {
            Format = Sdl.AudioU16,
            Callback = new(audio_callback),
        };
        _device = _sdl.OpenAudioDevice((string)null, 1, &audioSpec, ref sourceSpec, (int)Sdl.AudioAllowAnyChange);

        if(sourceSpec.Freq != WaveFormat.SampleRate || sourceSpec.Channels != WaveFormat.Channels)

        if (_device == 0)
        {
            RecordingStopped?.Invoke(this, new StoppedEventArgs(_sdl.GetErrorAsException()));
            return;
        }
        _sdl.PauseAudioDevice(_device, 0);
    }
    private void audio_callback(void* userdata, byte* stream, int len)
    {
        if (len != 0)
        {
            var buffer = new byte[len];
            Marshal.Copy(new(stream), buffer, 0, len);



            DataAvailable?.Invoke(this, new WaveInEventArgs(buffer, len));
        }
    }

    public void StopRecording()
    {
        _sdl?.PauseAudioDevice(_device, 1);
        _sdl?.CloseAudio();
        _sdl?.Quit();
        _sdl?.Dispose();
        RecordingStopped?.Invoke(this, new StoppedEventArgs());
    }
}
