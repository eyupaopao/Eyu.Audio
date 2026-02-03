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
            capture.StartRecording();
        }
        catch
        {
            throw;
        }
    }

    public new void StopRecording()
    {
        capture.StopRecording();
    }

    public event EventHandler<WaveInEventArgs> DataAvailable
    {
        add
        {
            capture.DataAvailable += value;
        }
        remove
        {
            capture.DataAvailable -= value;
        }
    }



    public event EventHandler<StoppedEventArgs> RecordingStopped
    {
        add
        {
            capture.RecordingStopped += value;
        }
        remove
        {
            capture.RecordingStopped -= value;
        }
    }
    IWaveIn capture;
    public WaveFormat WaveFormat { get; set; }


}
