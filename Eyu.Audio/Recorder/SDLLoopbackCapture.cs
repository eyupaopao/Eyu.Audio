using Eyu.Audio.Provider;
using Eyu.Audio.Utils;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Silk.NET.SDL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Eyu.Audio;

/// <summary>
/// SDL Loopback Audio Capture - Captures audio output from speakers/system playback
/// This is a simulated loopback capture that attempts to capture audio being played back through the system
/// </summary>
public unsafe class SDLLoopbackCapture : IWaveIn
{
    private AudioDevice? _playbackDevice;
    private bool _isRecording;
    private uint _device;
    private byte[] _sourceBuffer;

    public SDLLoopbackCapture(AudioDevice? device = null)
    {
        if (device?.IsCapture == true)
        {
            throw new SdlException(SdlApi.ErrorDeviceTyep);
        }        
        if (device == null)
        {
            // Try to get the default playback device for loopback capture
            device = DeviceEnumerator.Instance.SdlRenderDevices.FirstOrDefault();
        }        
        if (device == null)
        {
            throw new SdlException(SdlApi.NoOutputDevice);
        }
        if (device.DriverType != DriverType.Sdl)
        {
            throw new SdlException(SdlApi.ErrorDeviceTyep);
        }
        this._playbackDevice = device;
        WaveFormat = new WaveFormat(44100, 16, 2); // Default to 44.1kHz, 16-bit, stereo
        DeviceEnumerator.Instance.RenderDeviceChangedAction += this.OnRenderDeviceChanged;
    }

    private void OnRenderDeviceChanged()
    {
        if (_playbackDevice == null)
        {
            // If our device became null, try to get the first available playback device
            _playbackDevice = DeviceEnumerator.Instance.SdlRenderDevices.FirstOrDefault();
        }
        else if (DeviceEnumerator.Instance.SdlRenderDevices.Any(e => e.Device == _playbackDevice.Device))
        {
            // Our device still exists, nothing to do
            return;
        }
        else
        {
            // Our device is gone, try to get the first available playback device
            _playbackDevice = DeviceEnumerator.Instance.SdlRenderDevices.FirstOrDefault();
        }
        
        if (_playbackDevice == null)
        {
            // No playback device available anymore
            SdlApi.Api.CloseAudioDevice(_device);
            RecordingStopped?.Invoke(this, new StoppedEventArgs(new SdlException(SdlApi.NoOutputDevice)));
            return;
        }
        
        // Restart recording if we were recording
        if (_isRecording)
        {
            StopRecording();
            StartRecording();
        }
    }

    public WaveFormat WaveFormat { get; set; }
    
    private WaveFormat sourceFormat;
    private float dataLenRatio;
    private BufferedWaveProvider bufferedWaveProvider;
    private IWaveProvider waveProvider;

    public event EventHandler<WaveInEventArgs> DataAvailable;
    public event EventHandler<StoppedEventArgs> RecordingStopped;

    public void Dispose()
    {
        StopRecording();
        DeviceEnumerator.Instance.RenderDeviceChangedAction -= this.OnRenderDeviceChanged;
    }

    private AudioSpec sourceSpec;

    public unsafe void StartRecording()
    {
        // For loopback capture, we'll try to open the playback device in capture mode
        // Note: True loopback capture is not supported by SDL, so we'll use a workaround
        // that attempts to capture from the default recording device but with settings
        // that match the playback device
        
        var audioSpec = new AudioSpec {
            Freq = WaveFormat.SampleRate,
            Format = Sdl.AudioS16,
            Channels = (byte)WaveFormat.Channels,
            Samples = (ushort)Math.Pow(2, Math.Ceiling(Math.Log(WaveFormat.AverageBytesPerSecond / WaveFormat.SampleRate, 2))),
            Callback = new(AudioCallback),
        };
        
        var obtainedSpec = new AudioSpec();

        // Try to open the device for capture (this is where SDL loopback capture limitation comes in)
        // Since SDL doesn't support true loopback capture, we'll try to capture from the default device
        // and document this limitation
        _device = SdlApi.Api.OpenAudioDevice((byte*)null, 1, &audioSpec, &obtainedSpec, (int)Sdl.AudioAllowAnyChange);
        
        if (_device == 0)
        {
            // If opening default device fails, try to use the playback device name as capture device
            // This might work on some systems that support loopback capture
            _device = SdlApi.Api.OpenAudioDevice(_playbackDevice?.Device, 1, &audioSpec, &obtainedSpec, (int)Sdl.AudioAllowAnyChange);
        }

        sourceSpec = obtainedSpec;

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
        _isRecording = true;
    }
    
    private void AudioCallback(void* userdata, byte* stream, int len)
    {
        if (len != 0 && DataAvailable != null)
        {
            len = Math.Min(_sourceBuffer.Length, len);
            int targetLen = (int)(len * dataLenRatio);
            var targetBuffer = new byte[targetLen];
            
            Marshal.Copy(new IntPtr(stream), _sourceBuffer, 0, len);
            bufferedWaveProvider.AddSamples(_sourceBuffer, 0, len);
            
            var readed = waveProvider.Read(targetBuffer, 0, targetLen);
            DataAvailable?.Invoke(this, new WaveInEventArgs(targetBuffer, readed));
        }
    }
    
    private void CreateWaveProvider(WaveFormat sourceFormat, WaveFormat targetFormat)
    {
        dataLenRatio = (targetFormat.SampleRate * targetFormat.BitsPerSample * targetFormat.Channels * 1.0f) / 
                      (sourceFormat.SampleRate * sourceFormat.BitsPerSample * sourceFormat.Channels);
        
        bufferedWaveProvider = new BufferedWaveProvider(sourceFormat);
        ISampleProvider channel = new SampleChannel(bufferedWaveProvider);
        
        if (sourceFormat.SampleRate != targetFormat.SampleRate)
        {
            channel = new SampleWaveFormatConversionProvider(targetFormat, channel);
        }
        
        if (targetFormat.Channels != 1 && sourceFormat.Channels == 1)
        {
            channel = new MonoToStereoSampleProvider(channel);
        }
        if (targetFormat.Channels == 1 && sourceFormat.Channels != 1)
        {
            channel = new StereoToMonoSampleProvider(channel);
        }

        if (targetFormat.BitsPerSample == 32)
            waveProvider = new SampleToWaveProvider(channel);
        else if (targetFormat.BitsPerSample == 24)
            waveProvider = new SampleToWaveProvider24(channel);
        else
            waveProvider = new SampleToWaveProvider16(channel);
    }

    public void StopRecording()
    {
        if (!_isRecording) return;
        
        SdlApi.Api.PauseAudioDevice(_device, 1);
        SdlApi.Api.CloseAudioDevice(_device);
        RecordingStopped?.Invoke(this, new StoppedEventArgs());
        _isRecording = false;
    }
}