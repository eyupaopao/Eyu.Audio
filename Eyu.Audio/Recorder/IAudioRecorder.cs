using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eyu.Audio.Recorder
{
    public interface IAudioRecorder : IDisposable
    {
        event EventHandler<WaveInEventArgs> DataAvailable;

        event EventHandler<StoppedEventArgs> RecordingStopped;

        void StartRecord();
        void StopRecord();

        WaveFormat WaveFormat
        {
            get;
        }
    }

}
