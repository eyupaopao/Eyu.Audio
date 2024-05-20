using Eyu.Audio.Alsa;
using NAudio.Wave;
using System;
using System.Threading;

namespace Eyu.Audio.Recorder;

public class AlsaCapture : IWaveIn
{
    public AlsaCapture(bool loopback = false)
    {
        WaveFormat = new WaveFormat(8000, 16, 2);
        this.loopback = loopback;
    }
    private ISoundDevice? alsaDevice;
    bool loopback = false;

    public WaveFormat WaveFormat
    {
        get; set;
    }

    public event EventHandler<WaveInEventArgs>? DataAvailable;
    public event EventHandler<StoppedEventArgs>? RecordingStopped;


    public void StartRecording()
    {
        try
        {
            alsaDevice = AlsaDeviceBuilder.Create(new SoundDeviceSettings() {
                RecordingBitsPerSample = (ushort)WaveFormat.BitsPerSample,
                RecordingChannels = (ushort)WaveFormat.Channels,
                RecordingSampleRate = (ushort)WaveFormat.SampleRate,
                RecordingDeviceName = loopback ? "dmix" : "default",
            });
            alsaDevice.Record((buffer) =>
            {
                DataAvailable?.Invoke(this, new WaveInEventArgs(buffer, buffer.Length));
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            RecordingStopped?.Invoke(this, new StoppedEventArgs(ex));
        }
    }
    public void StopRecording()
    {
        alsaDevice?.Stop();
        alsaDevice?.Dispose();
        RecordingStopped?.Invoke(this, new StoppedEventArgs());
    }

    public void Dispose()
    {
        StopRecording();
    }


}
