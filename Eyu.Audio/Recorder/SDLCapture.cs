using Eyu.Audio.Provider;
using Eyu.Audio.Utils;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
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

    public SDLCapture(int deviceIndex = -1)
    {
        WaveFormat = new WaveFormat(8000, 16, 1);
        this.deviceIndex = deviceIndex;
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
    private readonly int deviceIndex;

    public unsafe void StartRecording()
    {
        if (SdlApi.InitAudio() != 0)
        {
            RecordingStopped?.Invoke(this, new StoppedEventArgs(SdlApi.GetErrorAsException()));
            return;
        }
        var audioSpec = new AudioSpec {
            Freq = WaveFormat.SampleRate,
            Format = Sdl.AudioS16Sys,
            Callback = new(audio_callback),
        };
        var audioSpec1 = new AudioSpec();
        byte* deviceName = null;
        if (deviceIndex > -1)
        {
            deviceName = SdlApi.GetAudioDeviceName(deviceIndex, 1);
        }
        _device = SdlApi.OpenAudioDevice(deviceName, 1, &audioSpec, &audioSpec1);
        sourceSpec = audioSpec1;

        if (_device == 0)
        {
            RecordingStopped?.Invoke(this, new StoppedEventArgs(SdlApi.GetErrorAsException()));
            return;
        }
        var bitsPerSample = sourceSpec.Size / sourceSpec.Samples / sourceSpec.Channels * 8;
        _sourceBuffer = new byte[sourceSpec.Size * 2];
        sourceFormat = new WaveFormat(sourceSpec.Freq, (int)bitsPerSample, sourceSpec.Channels);
        InitProvider(sourceFormat, WaveFormat);

        SdlApi.PauseAudioDevice(_device, 0);
    }
    private void audio_callback(void* userdata, byte* stream, int len)
    {
        if (len != 0 && DataAvailable != null)
        {
            len = Math.Min(_sourceBuffer.Length, len);
            int targetLen = (int)(2 * len * dataLenRatio);
            var targetBuffer = new byte[targetLen];
            Marshal.Copy(new(stream), _sourceBuffer, 0, len);
            bufferedWaveProvider?.AddSamples(_sourceBuffer, 0, len);
            var readed = waveProvider.Read(targetBuffer, 0, targetLen);

            DataAvailable?.Invoke(this, new WaveInEventArgs(targetBuffer, readed));
        }
    }
    void InitProvider(WaveFormat sourceFormat, WaveFormat targetFormat)
    {
        dataLenRatio = (WaveFormat.SampleRate * WaveFormat.BitsPerSample * WaveFormat.Channels * 1.0f) / (sourceFormat.SampleRate * sourceFormat.BitsPerSample * sourceFormat.Channels);
        bufferedWaveProvider = new BufferedWaveProvider(sourceFormat);
        ISampleProvider channle = new SampleChannel(bufferedWaveProvider, true);
        if (sourceFormat.SampleRate != targetFormat.SampleRate)
        {
            channle = new SampleWaveFormatConversionProvider(WaveFormat, channle);
        }
        if (targetFormat.Channels == 1)
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
        SdlApi.PauseAudioDevice(_device, 1);
        SdlApi.CloseAudioDevice(_device);
        RecordingStopped?.Invoke(this, new StoppedEventArgs());
    }
}
