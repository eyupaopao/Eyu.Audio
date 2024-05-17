using Eyu.Audio.Timer;
using NAudio.Wave;
using OpenTK.Audio.OpenAL;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Eyu.Audio.Recorder;

public class ALRecorder : IWaveIn
{
    public ALRecorder(WaveFormat waveFormat, string? deviceName = null)
    {
        WaveFormat = waveFormat;
        this.deviceName = deviceName;
    }

    private byte[] buffer;
    private int Period = 100;

    private readonly string deviceName;
    HighResolutionTimer timer;

    public WaveFormat WaveFormat
    {
        get; set;
    }


    public event EventHandler<WaveInEventArgs> DataAvailable;
    public event EventHandler<StoppedEventArgs> RecordingStopped;
    CancellationTokenSource cts;

    public void Dispose()
    {
        cts?.Cancel();
    }

    public static IEnumerable<string> GetDevices()
    {
        return ALC.GetStringList(GetEnumerationStringList.DeviceSpecifier);
    }
    public static IEnumerable<string> get()
    {
        return ALC.GetStringList(GetEnumerationStringList.CaptureDeviceSpecifier);
    }

    public void StartRecording()
    {
        buffer = new byte[WaveFormat.SampleRate * WaveFormat.Channels * 2 * Period / 1000];
        var captureDevice = ALC.CaptureOpenDevice(deviceName, WaveFormat.SampleRate, ALFormat.Stereo16, WaveFormat.SampleRate * Period / 1000);
        cts = new CancellationTokenSource();
        ALError error = AL.GetError();
        if (error != ALError.NoError)
        {
            throw new Exception(AL.GetErrorString(error));
        }
        ALC.CaptureStart(captureDevice);
        timer = new HighResolutionTimer(() =>
        {
            if (cts.IsCancellationRequested)
            {
                ALC.CaptureStop(captureDevice);
                RecordingStopped?.Invoke(null, new StoppedEventArgs());
                timer?.Stop();
            }
            try
            {

                int samplesAvailable = ALC.GetInteger(captureDevice, AlcGetInteger.CaptureSamples);
                if (samplesAvailable > 480)
                {
                    int samplesToRead = Math.Min(samplesAvailable, buffer.Length);
                    ALC.CaptureSamples(captureDevice, buffer, samplesToRead);
                    var size = samplesToRead * WaveFormat.Channels * 2;
                    var data = new byte[size];
                    Array.Copy(buffer, 0, data, 0, size);
                    DataAvailable?.Invoke(null, new WaveInEventArgs(data, size));
                }

            }
            catch (Exception ex)
            {

            }

        });
        timer.SetPeriod(Period - 10);
        timer.Start();
    }

    public void StopRecording()
    {
        cts.Cancel();
    }
}

public enum RecorerType
{
    Capture, Loopback,
}