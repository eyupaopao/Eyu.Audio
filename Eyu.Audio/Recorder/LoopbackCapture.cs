using NAudio.CoreAudioApi;
using NAudio.Wave.SampleProviders;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eyu.Audio.Recorder
{

    public class LoopbackCapture : WasapiCapture, IAudioRecorder
    {
        public LoopbackCapture(WaveFormat waveFormat) : base(WasapiLoopbackCapture.GetDefaultLoopbackCaptureDevice(), false, 100)
        {
            capture = this;
            capture.WaveFormat = waveFormat;

        }
        protected override AudioClientStreamFlags GetAudioClientStreamFlags()
        {
            return AudioClientStreamFlags.Loopback | base.GetAudioClientStreamFlags();
        }

        public void StartRecord()
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

        public void StopRecord()
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
        public WaveFormat WaveFormat => capture.WaveFormat;


    }
}
