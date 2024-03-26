using Eyu.Audio.Alsa;
using NAudio.Wave;
using System;
using System.Threading;

namespace Eyu.Audio.Recorder;

public class AlsaRecorder : IAudioRecorder
{
    private ISoundDevice alsaDevice;

    public WaveFormat WaveFormat
    {
        get;
    }

    public event EventHandler<WaveInEventArgs> DataAvailable;
    public event EventHandler<StoppedEventArgs> RecordingStopped;


    public AlsaRecorder(WaveFormat waveFormat)
    {
        WaveFormat = waveFormat;
        alsaDevice = AlsaDeviceBuilder.Create(new SoundDeviceSettings()
        {
            RecordingBitsPerSample = (ushort)WaveFormat.BitsPerSample,
            RecordingChannels = (ushort)WaveFormat.Channels,
            RecordingSampleRate = (ushort)WaveFormat.SampleRate,
        });
    }

    CancellationTokenSource cts;

    public void StartRecord()
    {
        cts = new CancellationTokenSource();


        alsaDevice.Record((buffer) =>
        {
            DataAvailable?.Invoke(this, new WaveInEventArgs(buffer, buffer.Length));
        }, CancellationToken.None);
    }
    public void StopRecord()
    {
        cts.Cancel();
    }

    public void Dispose()
    {
        cts?.Cancel();
    }


}
