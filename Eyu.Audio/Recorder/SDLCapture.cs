using Eyu.Audio.Provider;
using Eyu.Audio.Utils;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Silk.NET.SDL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Eyu.Audio.Recorder;

public unsafe class SDLCapture : IWaveIn
{

    public SDLCapture(SDLDevice? device = null)
    {
        if (device != null && device.Capture != 1)
        {
            throw new SdlException(SdlApi.ErrorDeviceTyep);
        }
        if (device == null)
        {
            device = SdlApi.GetDevices(1).FirstOrDefault();
        }
        if (device == null)
        {
            throw new SdlException(SdlApi.NoInputDevice);
        }
        this.currentDevice = device;
        WaveFormat = new WaveFormat(8000, 16, 1);
        SdlApi.CaptureDeviceChanged += this.SdlApi_CaptureDeviceChanged;
    }

    private void SdlApi_CaptureDeviceChanged(object? sender, IEnumerable<SDLDevice> e)
    {
        if (currentDevice == null)
        {
            currentDevice = SdlApi.GetDevices(1).FirstOrDefault();
        }
        else if (e.Any(e => e.Name == currentDevice.Name))
        {
            return;
        }
        else
        {
            currentDevice = SdlApi.GetDevices(1).FirstOrDefault();
        }
        if (currentDevice == null)
        {
            SdlApi.Api.CloseAudioDevice(_device);
            RecordingStopped?.Invoke(this, new StoppedEventArgs(new SdlException(SdlApi.NoInputDevice)));
            return;
        }
        StopRecording();
        StartRecording();
    }

    private uint _device;
    private byte[] _sourceBuffer;

    public WaveFormat WaveFormat
    {
        get;
        set;
    }
    public WaveFormat sourceFormat;
    float dataLenRatio;
    BufferedWaveProvider bufferedWaveProvider;
    IWaveProvider waveProvider;

    public event EventHandler<WaveInEventArgs> DataAvailable;
    public event EventHandler<StoppedEventArgs> RecordingStopped;

    public void Dispose()
    {
        StopRecording();
    }
    AudioSpec sourceSpec;
    private SDLDevice? currentDevice;

    public unsafe void StartRecording()
    {
        var audioSpec = new AudioSpec {
            Freq = WaveFormat.SampleRate,
            Format = Sdl.AudioF32,
            Callback = new(AudioCallback),
        };
        var audioSpec1 = new AudioSpec();

        _device = SdlApi.Api.OpenAudioDevice(currentDevice == null ? null : currentDevice.Name, 0, &audioSpec, &audioSpec1, (int)Sdl.AudioAllowAnyChange);
        sourceSpec = audioSpec1;

        if (_device == 0)
        {
            RecordingStopped?.Invoke(this, new StoppedEventArgs(SdlApi.Api.GetErrorAsException()));
            return;
        }
        _sourceBuffer = new byte[sourceSpec.Size * 2];
        if (sourceSpec.Format is Sdl.AudioF32 or Sdl.AudioF32Lsb or Sdl.AudioF32Msb or Sdl.AudioF32Sys)
        {
            sourceFormat = WaveFormat.CreateIeeeFloatWaveFormat(sourceSpec.Freq, sourceSpec.Channels);
        }
        else
        {
            var bitsPerSample = sourceSpec.Size / sourceSpec.Samples / sourceSpec.Channels * 8;
            sourceFormat = new WaveFormat(sourceSpec.Freq, (int)bitsPerSample, sourceSpec.Channels);
        }
        CreateWaveProvider(sourceFormat, WaveFormat);

        SdlApi.Api.PauseAudioDevice(_device, 0);
    }
    private void AudioCallback(void* userdata, byte* stream, int len)
    {
        if (len != 0 && DataAvailable != null)
        {
            len = Math.Min(_sourceBuffer.Length, len);
            int targetLen = (int)(len * dataLenRatio);
            var targetBuffer = new byte[targetLen];
            Marshal.Copy(new(stream), _sourceBuffer, 0, len);
            bufferedWaveProvider?.AddSamples(_sourceBuffer, 0, len);
            var readed = waveProvider.Read(targetBuffer, 0, targetLen);
            DataAvailable?.Invoke(this, new WaveInEventArgs(targetBuffer, readed));
        }
    }
    void CreateWaveProvider(WaveFormat sourceFormat, WaveFormat targetFormat)
    {
        dataLenRatio = (WaveFormat.SampleRate * WaveFormat.BitsPerSample * WaveFormat.Channels * 1.0f) / (sourceFormat.SampleRate * sourceFormat.BitsPerSample * sourceFormat.Channels);
        bufferedWaveProvider = new BufferedWaveProvider(sourceFormat);
        ISampleProvider channle = new SampleChannel(bufferedWaveProvider);
        if (sourceFormat.SampleRate != targetFormat.SampleRate)
        {
            channle = new SampleWaveFormatConversionProvider(WaveFormat, channle);
        }
        if (targetFormat.Channels != 1 && sourceFormat.Channels == 1)
        {
            channle = new MonoToStereoSampleProvider(channle);
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
        SdlApi.Api.PauseAudioDevice(_device, 1);
        SdlApi.Api.CloseAudioDevice(_device);
        RecordingStopped?.Invoke(this, new StoppedEventArgs());
    }
}
