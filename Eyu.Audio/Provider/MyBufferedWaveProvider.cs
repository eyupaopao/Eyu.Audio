using NAudio.Utils;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Eyu.Audio.Provider
{
    /// <summary>
    /// Provides a buffered store of samples
    /// Read method will return queued samples or fill buffer with zeroes
    /// Now backed by a circular buffer
    /// </summary>
    public class MyBufferedWaveProvider : IWaveProvider
    {
        private CircularBuffer circularBuffer;
        private readonly WaveFormat waveFormat;

        /// <summary>
        /// Creates a new buffered WaveProvider
        /// </summary>
        /// <param name="waveFormat">WaveFormat</param>
        public MyBufferedWaveProvider(WaveFormat waveFormat)
        {
            this.waveFormat = waveFormat;
            BufferLength = waveFormat.AverageBytesPerSecond * 5;
            ReadFully = true;
            //波形计算参数
            StreamVolumeData();
        }

        /// <summary>
        /// If true, always read the amount of data requested, padding with zeroes if necessary
        /// By default is set to true
        /// </summary>
        public bool ReadFully
        {
            get; set;
        }

        /// <summary>
        /// Buffer length in bytes
        /// </summary>
        public int BufferLength
        {
            get; set;
        }

        /// <summary>
        /// Buffer duration
        /// </summary>
        public TimeSpan BufferDuration
        {
            get
            {
                return TimeSpan.FromSeconds((double)BufferLength / WaveFormat.AverageBytesPerSecond);
            }
            set
            {
                BufferLength = (int)(value.TotalSeconds * WaveFormat.AverageBytesPerSecond);
            }
        }

        /// <summary>
        /// If true, when the buffer is full, start throwing away data
        /// if false, AddSamples will throw an exception when buffer is full
        /// </summary>
        public bool DiscardOnBufferOverflow
        {
            get; set;
        }

        /// <summary>
        /// The number of buffered bytes
        /// </summary>
        public int BufferedBytes
        {
            get
            {
                return circularBuffer is null ? 0 : circularBuffer.Count;
            }
        }

        /// <summary>
        /// Buffered Duration
        /// </summary>
        public TimeSpan BufferedDuration
        {
            get
            {
                return TimeSpan.FromSeconds((double)BufferedBytes / WaveFormat.AverageBytesPerSecond);
            }
        }

        /// <summary>
        /// Gets the WaveFormat
        /// </summary>
        public WaveFormat WaveFormat
        {
            get
            {
                return waveFormat;
            }
        }
        public Action<CircularBuffer> bufferDelegate;
        /// <summary>
        /// Adds samples. Takes a copy of buffer, so that buffer can be reused if necessary
        /// </summary>
        public void AddSamples(byte[] buffer, int offset, int count)
        {
            // create buffer here to allow user to customise buffer length
            if (circularBuffer is null)
            {
                circularBuffer = new CircularBuffer(BufferLength);
            }

            bufferDelegate?.Invoke(circularBuffer);
            var written = circularBuffer.Write(buffer, offset, count);
            if (written < count && !DiscardOnBufferOverflow)
            {
                throw new InvalidOperationException("Buffer full");
            }
        }

        /// <summary>
        /// Reads from this WaveProvider
        /// Will always return count bytes, since we will zero-fill the buffer if not enough available
        /// </summary>
        public int Read(byte[] buffer, int offset, int count)
        {
            int read = 0;
            if (circularBuffer is not null) // not yet created
            {
                read = circularBuffer.Read(buffer, offset, count);
            }
            if (ReadFully && read < count)
            {
                // zero the end of the buffer
                Array.Clear(buffer, offset + read, count - read);
                read = count;
            }
            #region
            //转换为sample
            float[] sample = ToSample(buffer, count);
            //生成波形
            WaveFormCalculator(sample, 0, sample.Length);
            //截取pcmbuffer
            AudioBufferHandler?.Invoke(buffer);
            //截取samplebuffer
            SampleBufferHandler?.Invoke(sample);
            #endregion

            return read;
        }
        private float[] ToSample(byte[] sourceBuffer, int sourceBufferCount)
        {
            int targetBufferCount = sourceBufferCount / 2;
            float[] targetBuffer = new float[targetBufferCount];
            int outIndex = 0;
            for (int n = 0; n < sourceBufferCount; n += 2)
            {
                targetBuffer[outIndex++] = BitConverter.ToInt16(sourceBuffer, n) / 32768f;
            }
            return targetBuffer;
        }

        public Action<byte[]> AudioBufferHandler;
        public Action<float[]> SampleBufferHandler;

        #region 计算波形

        private float[] maxSamples;
        private int sampleCount;
        private int channels;
        private StreamVolumeEventArgs args;

        //波形参数初始化
        public void StreamVolumeData()
        {
            channels = WaveFormat.Channels;
            maxSamples = new float[channels];
            SamplesPerNotification = WaveFormat.SampleRate / 10;
            args = new StreamVolumeEventArgs() { MaxSampleValues = maxSamples };
        }

        public int SamplesPerNotification
        {
            get; set;
        }

        /// <summary>
        /// 计算结果用事件获取。
        /// </summary>
        public event EventHandler<StreamVolumeEventArgs> StreamVolume;
        private void WaveFormCalculator(float[] buffer, int offset, int samplesRead)
        {
            if (StreamVolume is not null)
            {
                for (int index = 0; index < samplesRead; index += channels)
                {
                    for (int channel = 0; channel < channels; channel++)
                    {
                        float sampleValue = Math.Abs(buffer[offset + index + channel]);
                        maxSamples[channel] = Math.Max(maxSamples[channel], sampleValue);
                    }
                    sampleCount++;
                    if (sampleCount >= SamplesPerNotification)
                    {
                        StreamVolume(this, args);
                        sampleCount = 0;
                        // n.b. we avoid creating new instances of anything here
                        Array.Clear(maxSamples, 0, channels);
                    }
                }
            }
        }
        #endregion

        /// <summary>
        /// Discards all audio from the buffer
        /// </summary>
        public void ClearBuffer()
        {
            if (circularBuffer is not null)
            {
                circularBuffer.Reset();
            }
        }
    }
}
