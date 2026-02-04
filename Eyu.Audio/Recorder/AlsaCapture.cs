using Eyu.Audio.Alsa;
using Eyu.Audio.Provider;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.IO;
using System.Threading;

namespace Eyu.Audio;

public class ALSACapture : IWaveIn
{
    public ALSACapture(Utils.AudioDevice? device = null,int audioBufferMillisecondsLength = 100)
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
        this.audioBufferMillisecondsLength = audioBufferMillisecondsLength;
        WaveFormat = new WaveFormat(8000, 16, 2);
    }
    private ALSAApi? alsaDevice;
    private Utils.AudioDevice? audioDevice;
    private readonly int audioBufferMillisecondsLength;
    private BufferedWaveProvider bufferedWaveProvider;
    private CancellationTokenSource cancellationTokenSource;
    public WaveFormat WaveFormat
    {
        get; set;
    }
    IWaveProvider waveProvider;

    float dataLenRatio;

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
            alsaDevice = new ALSAApi(settings);
            
            // Create wave provider for format conversion
            bufferedWaveProvider = new BufferedWaveProvider(WaveFormat);

            // Calculate buffer size based on source format
            int bufferSize = WaveFormat.AverageBytesPerSecond / 4; // Quarter second buffer
            cancellationTokenSource = new CancellationTokenSource();
            alsaDevice.Record((buffer) =>
            {
                // Add samples to the buffered provider for format conversion
                bufferedWaveProvider.AddSamples(buffer, 0, buffer.Length);

                // Check if we have enough data in the buffer based on audioBufferMillisecondsLength
                int requiredBytes = (int)((audioBufferMillisecondsLength / 1000.0) * WaveFormat.AverageBytesPerSecond);

                // Only raise DataAvailable when we have accumulated enough data
                if (bufferedWaveProvider.BufferedBytes >= requiredBytes)
                {
                    // Read converted samples according to target format
                    int targetLen = Math.Min(requiredBytes, bufferedWaveProvider.BufferedBytes);
                    var targetBuffer = new byte[targetLen];
                    var readed = bufferedWaveProvider.Read(targetBuffer, 0, targetLen);

                    // Raise the DataAvailable event with converted samples
                    DataAvailable?.Invoke(this, new WaveInEventArgs(targetBuffer, readed));
                }
            }, cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            RecordingStopped?.Invoke(this, new StoppedEventArgs(ex));
        }
    }
    void CreateWaveProvider(WaveFormat sourceFormat, WaveFormat targetFormat)
    {
        dataLenRatio = (targetFormat.SampleRate * targetFormat.BitsPerSample * targetFormat.Channels * 1.0f) / (sourceFormat.SampleRate * sourceFormat.BitsPerSample * sourceFormat.Channels);

        // 确保每次都创建新的 BufferedWaveProvider
        bufferedWaveProvider = new BufferedWaveProvider(sourceFormat);
        bufferedWaveProvider.DiscardOnBufferOverflow = true; // 防止缓冲区溢出

        ISampleProvider channle = new SampleChannel(bufferedWaveProvider);
        if (sourceFormat.SampleRate != targetFormat.SampleRate)
        {
            channle = new SampleWaveFormatConversionProvider(WaveFormat, channle);
        }
        if (targetFormat.Channels != 1 && sourceFormat.Channels == 1)
        {
            channle = new MonoToStereoSampleProvider(channle);
        }
        if (targetFormat.Channels == 1 && sourceFormat.Channels != 1)
        {
            channle = new StereoToMonoSampleProvider(channle);
        }

        if (targetFormat.BitsPerSample == 32)
            waveProvider = new SampleToWaveProvider(channle);
        else if (targetFormat.BitsPerSample == 24)
            waveProvider = new SampleToWaveProvider24(channle);
        else
            waveProvider = new SampleToWaveProvider16(channle);
    }

    public void StopRecording()
    {
        if (cancellationTokenSource?.IsCancellationRequested == true) return;
        try
        {
            cancellationTokenSource?.Cancel();            
            alsaDevice?.StopRecording();
            RecordingStopped?.Invoke(this, new StoppedEventArgs());
        }
        catch (Exception ex)
        {
            RecordingStopped?.Invoke(this, new StoppedEventArgs(ex));
        }
    }

    public void Dispose()
    {
        if (cancellationTokenSource?.IsCancellationRequested != true)
        {
            StopRecording();
        }
        
        // Ensure we clean up resources properly
        cancellationTokenSource?.Dispose();
    }
       

}
