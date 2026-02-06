using NAudio.CoreAudioApi;
using NAudio.Wave.SampleProviders;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eyu.Audio;


public class LoopbackCapture : WasapiCapture
{
    public LoopbackCapture() : base(WasapiLoopbackCapture.GetDefaultLoopbackCaptureDevice(), false, 100)
    {
    }

    protected override AudioClientStreamFlags GetAudioClientStreamFlags()
    {
        return AudioClientStreamFlags.Loopback | base.GetAudioClientStreamFlags();
    }

    public new void StartRecording()
    {
        try
        {
            base.StartRecording();
        }
        catch
        {
            throw;
        }
    }

    public new void StopRecording()
    {
        base.StopRecording();
    }

    public new event EventHandler<WaveInEventArgs>? DataAvailable
    {
        add => base.DataAvailable += value;
        remove => base.DataAvailable -= value;
    }

    public new event EventHandler<StoppedEventArgs>? RecordingStopped
    {
        add => base.RecordingStopped += value;
        remove => base.RecordingStopped -= value;
    }

    public new WaveFormat WaveFormat { get => base.WaveFormat; set => base.WaveFormat = value; }
}
