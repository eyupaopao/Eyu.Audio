using NAudio.Wave;
using System;

namespace Eyu.Audio.Recorder
{
    public class WaveInCapture : IAudioRecorder
    {
        public WaveInCapture(WaveFormat waveFormat)
        {
            //获取设备，用于调节声音。
            //var enumerator = new MMDeviceEnumerator();
            //mMDevice = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)[inputDeviceNumber];
            //mMDevice.
            capture = new WaveInEvent();
            capture.WaveFormat = waveFormat;

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
        public WaveFormat WaveFormat => capture.WaveFormat;

        IWaveIn capture;





        public void Dispose()
        {
            try
            {
                capture?.StopRecording();
                capture?.Dispose();
            }
            catch
            {
            }
            //mMDevice?.Dispose();
        }

        public void StopRecord()
        {
            capture.StopRecording();
        }
    }

}
