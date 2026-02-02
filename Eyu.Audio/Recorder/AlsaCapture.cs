using Eyu.Audio.Alsa;
using Eyu.Audio.Provider;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.IO;
using System.Threading;

namespace Eyu.Audio.Recorder;

public class AlsaCapture : IWaveIn
{
    public AlsaCapture(Utils.AudioDevice? device = null)
    {
        if (device?.IsCapture == false)
        {
            throw new ArgumentException("Device is not a capture device");
        }
        if (device == null)
        {
            // Use default device if none specified
            device = new Utils.AudioDevice { Device = "default", IsCapture = true };
        }
        this.audioDevice = device;
        WaveFormat = new WaveFormat(8000, 16, 2);
    }
    private ISoundDevice? alsaDevice;
    private FileStream stream;
    private Utils.AudioDevice? audioDevice;
    private WaveFormat sourceFormat;
    private float dataLenRatio;
    private BufferedWaveProvider bufferedWaveProvider;
    private IWaveProvider waveProvider;
    private byte[] sourceBuffer;

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
            // Get the actual device settings to determine the source format
            var settings = new SoundDeviceSettings() {
                RecordingBitsPerSample = (ushort)WaveFormat.BitsPerSample,
                RecordingChannels = (ushort)WaveFormat.Channels,
                RecordingSampleRate = (ushort)WaveFormat.SampleRate,
                RecordingDeviceName = audioDevice?.Device ?? "default",
            };
            
            // Create the device and get the actual source format
            alsaDevice = AlsaDeviceBuilder.Create(settings);
            sourceFormat = new WaveFormat((int)settings.RecordingSampleRate, (int)settings.RecordingBitsPerSample, (int)settings.RecordingChannels);
            
            // Create wave provider for format conversion
            CreateWaveProvider(sourceFormat, WaveFormat);
            
            // Calculate buffer size based on source format
            int bufferSize = sourceFormat.AverageBytesPerSecond / 4; // Quarter second buffer
            sourceBuffer = new byte[bufferSize];
            
            alsaDevice.Record((buffer) =>
            {
                // Add samples to the buffered provider for format conversion
                bufferedWaveProvider.AddSamples(buffer, 0, buffer.Length);
                
                // Read converted samples according to target format
                int targetLen = (int)(buffer.Length * dataLenRatio);
                var targetBuffer = new byte[targetLen];
                var readed = waveProvider.Read(targetBuffer, 0, targetLen);
                
                // Raise the DataAvailable event with converted samples
                DataAvailable?.Invoke(this, new WaveInEventArgs(targetBuffer, readed));
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
                RecordingDeviceName = audioDevice?.Device ?? "default",
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

    void CreateWaveProvider(WaveFormat sourceFormat, WaveFormat targetFormat)
    {
        dataLenRatio = (targetFormat.SampleRate * targetFormat.BitsPerSample * targetFormat.Channels * 1.0f) / 
                      (sourceFormat.SampleRate * sourceFormat.BitsPerSample * sourceFormat.Channels);
        bufferedWaveProvider = new BufferedWaveProvider(sourceFormat);
        ISampleProvider channel = new SampleChannel(bufferedWaveProvider);
        
        // Sample rate conversion
        if (sourceFormat.SampleRate != targetFormat.SampleRate)
        {
            channel = new SampleWaveFormatConversionProvider(targetFormat, channel);
        }
        
        // Channel conversion: mono to stereo or stereo to mono
        if (targetFormat.Channels != 1 && sourceFormat.Channels == 1)
        {
            channel = new MonoToStereoSampleProvider(channel);
        }
        if (targetFormat.Channels == 1 && sourceFormat.Channels != 1)
        {
            channel = new StereoToMonoSampleProvider(channel);
        }

        // Bits per sample conversion
        if (targetFormat.BitsPerSample == 32)
            waveProvider = new SampleToWaveProvider(channel);
        else if (targetFormat.BitsPerSample == 24)
            waveProvider = new SampleToWaveProvider24(channel);
        else
            waveProvider = new SampleToWaveProvider16(channel);
    }


}
