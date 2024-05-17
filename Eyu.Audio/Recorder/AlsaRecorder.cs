using Eyu.Audio.Alsa;
using NAudio.Wave;
using System;
using System.Threading;

namespace Eyu.Audio.Recorder;

public class AlsaRecorder : IWaveIn
{
    private ISoundDevice alsaDevice;

    public WaveFormat WaveFormat
    {
        get; set;
    }

    public event EventHandler<WaveInEventArgs> DataAvailable;
    public event EventHandler<StoppedEventArgs> RecordingStopped;



    public void StartRecording()
    {
        alsaDevice = AlsaDeviceBuilder.Create(new SoundDeviceSettings() {
            RecordingBitsPerSample = (ushort)WaveFormat.BitsPerSample,
            RecordingChannels = (ushort)WaveFormat.Channels,
            RecordingSampleRate = (ushort)WaveFormat.SampleRate,
        });
        alsaDevice.Record((buffer) =>
        {
            DataAvailable?.Invoke(this, new WaveInEventArgs(buffer, buffer.Length));
        }, CancellationToken.None);
    }
    public void StopRecording()
    {
        alsaDevice?.Stop();
        alsaDevice?.Dispose();
    }

    public void Dispose()
    {
        StopRecording();
    }


}
