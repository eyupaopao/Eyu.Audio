using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;

namespace Eyu.Audio.Provider
{
    /// <summary>
    /// Converts an IWaveProvider containing 16 bit PCM to an
    /// ISampleProvider
    /// </summary>
    public class MyPcm16BitToSampleProvider : SampleProviderConverterBase
    {
        /// <summary>
        /// Initialises a new instance of Pcm16BitToSampleProvider
        /// </summary>
        /// <param name="source">Source wave provider</param>
        public MyPcm16BitToSampleProvider(IWaveProvider source)
            : base(source)
        {
            StreamVolumeData();
        }

        /// <summary>
        /// Reads samples from this sample provider
        /// </summary>
        /// <param name="buffer">Sample buffer</param>
        /// <param name="offset">Offset into sample buffer</param>
        /// <param name="count">Samples required</param>
        /// <returns>Number of samples read</returns>
        public override int Read(float[] buffer, int offset, int count)
        {
            int sourceBytesRequired = count * 2;

            EnsureSourceBuffer(sourceBytesRequired);


            int bytesRead = source.Read(sourceBuffer, 0, sourceBytesRequired);
            int outIndex = offset;
            for (int n = 0; n < bytesRead; n += 2)
            {
                buffer[outIndex++] = BitConverter.ToInt16(sourceBuffer, n) / 32768f;
            }

            //波形
            WaveFormCalculator(buffer, offset, count);

            return bytesRead / 2;
        }

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
    }
}
