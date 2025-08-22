using NAudio.Utils;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eyu.Audio.Provider
{
    public class MySampleToWaveProvider16 : IWaveProvider
    {
        private readonly ISampleProvider sourceProvider;
        private readonly WaveFormat waveFormat;
        private volatile float volume;
        private float[] sourceBuffer;
        public Action<byte[], int> AudioBufferHandler;
        public Action<float[], int> SampleBufferHandler;

        /// <summary>
        /// Converts from an ISampleProvider (IEEE float) to a 16 bit PCM IWaveProvider.
        /// Number of channels and sample rate remain unchanged.
        /// </summary>
        /// <param name="sourceProvider">The input source provider</param>
        public MySampleToWaveProvider16(ISampleProvider sourceProvider)
        {
            if (sourceProvider.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
                throw new ArgumentException("Input source provider must be IEEE float", nameof(sourceProvider));
            if (sourceProvider.WaveFormat.BitsPerSample != 32)
                throw new ArgumentException("Input source provider must be 32 bit", nameof(sourceProvider));

            waveFormat = new WaveFormat(sourceProvider.WaveFormat.SampleRate, 16, sourceProvider.WaveFormat.Channels);
            //_outputWaveFormat = new WaveFormat(48000, 16, 2);

            this.sourceProvider = sourceProvider;
            volume = 1.0f;
            StreamVolumeData();
        }

        /// <summary>
        /// Reads bytes from this wave stream
        /// </summary>
        /// <param name="destBuffer">The destination buffer</param>
        /// <param name="offset">Offset into the destination buffer</param>
        /// <param name="numBytes">Number of bytes read</param>
        /// <returns>Number of bytes read.</returns>
        public int Read(byte[] destBuffer, int offset, int numBytes)
        {
            int samplesRequired = numBytes / 2;
            sourceBuffer = BufferHelpers.Ensure(sourceBuffer, samplesRequired);
            int sourceSamples = sourceProvider.Read(sourceBuffer, 0, samplesRequired);
            var destWaveBuffer = new WaveBuffer(destBuffer);

            int destOffset = offset / 2;
            for (int sample = 0; sample < sourceSamples; sample++)
            {
                // adjust volume
                float sample32 = sourceBuffer[sample] * volume;
                // clip
                if (sample32 > 1.0f)
                    sample32 = 1.0f;
                if (sample32 < -1.0f)
                    sample32 = -1.0f;
                destWaveBuffer.ShortBuffer[destOffset++] = (short)(sample32 * 32767);
            }

            //截取floatsample
            SampleBufferHandler?.Invoke(sourceBuffer, sourceSamples);
            WaveFormCalculator(sourceBuffer, 0, sourceSamples);
            //截取pcmbuffer
            AudioBufferHandler?.Invoke(destBuffer, sourceSamples * 2);

            return sourceSamples * 2;
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
        /// <summary>
        /// <see cref="IWaveProvider.WaveFormat"/>
        /// </summary>
        public WaveFormat WaveFormat => waveFormat;


        /// <summary>
        /// Volume of this channel. 1.0 = full scale
        /// </summary>
        public float Volume
        {
            get
            {
                return volume;
            }
            set
            {
                volume = value;
            }
        }
    }
}
