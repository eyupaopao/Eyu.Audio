using Eyu.Audio.Alsa;
using NAudio.Wave;
using System;
using System.IO;
using System.Threading;

namespace Eyu.Audio.Recorder;

public class AlsaCapture : IWaveIn
{
    public AlsaCapture(string? deviceName = null)
    {
        WaveFormat = new WaveFormat(8000, 16, 2);
        this.deviceName = deviceName;
    }
    private ISoundDevice? alsaDevice;
    private FileStream stream;
    private readonly string deviceName;

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
                RecordingDeviceName = deviceName == null ? "default" : deviceName,
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

    public void StartRecording(string fileName)
    {
        try
        {
            alsaDevice = AlsaDeviceBuilder.Create(new SoundDeviceSettings() {
                RecordingBitsPerSample = (ushort)WaveFormat.BitsPerSample,
                RecordingChannels = (ushort)WaveFormat.Channels,
                RecordingSampleRate = (ushort)WaveFormat.SampleRate,
                RecordingDeviceName = deviceName == null ? "default" : deviceName,
            });
            stream = File.OpenWrite(fileName);
            alsaDevice.Record(stream, CancellationToken.None);
        }
        catch (Exception ex)
        {
            stream?.Close();
            stream?.Dispose();
            RecordingStopped?.Invoke(this, new StoppedEventArgs(ex));
        }

    }
    public void StopRecording()
    {
        alsaDevice?.Stop();
        alsaDevice?.Dispose();
        stream?.Close();
        stream?.Dispose();
        RecordingStopped?.Invoke(this, new StoppedEventArgs());
    }

    public void Dispose()
    {
        StopRecording();
    }


}
