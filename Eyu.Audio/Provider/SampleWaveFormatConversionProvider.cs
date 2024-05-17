using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NWaves.Operations;
using NWaves.Signals;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eyu.Audio.Provider
{
    /// <summary>
    /// 对float音频进行重采样
    /// </summary>
    public class SampleWaveFormatConversionProvider : ISampleProvider, IDisposable
    {
        private readonly ISampleProvider sourceProvider;

        public SampleWaveFormatConversionProvider(WaveFormat targetFormat, ISampleProvider sourceProvider)
        {
            if (sourceProvider.WaveFormat.Channels == 1)
                this.sourceProvider = new MonoToStereoSampleProvider(sourceProvider);
            else
                this.sourceProvider = sourceProvider;

            sourceFormat = sourceProvider.WaveFormat;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(targetFormat.SampleRate, targetFormat.Channels);

        }
        public WaveFormat WaveFormat
        {
            get;
        }

        private WaveFormat sourceFormat
        {
            get;
        }

        public void Dispose()
        {
        }

        public int Read(float[] buffer, int offset, int count)
        {
            // 计算实际需要读取的长度
            var readCount = (int)(sourceFormat.SampleRate / (double)WaveFormat.SampleRate * count);
            // 如果更长，说明采样率变低了，使用原长度，保证不溢出。
            if (readCount > count) readCount = count;
            // 读取需要的音频
            int readedLen = sourceProvider.Read(buffer, offset, readCount);
            // 读取结束。
            if (readedLen == 0)
            {
                return readedLen;
            }
            // 实际读取到的采样数。
            int readedSamples = readedLen / sourceFormat.Channels;
            // 左声道
            var left = new float[readedSamples];
            // 右声道
            var right = new float[readedSamples];
            for (int i = 0; i < readedSamples; i++)
            {
                left[i] = buffer[i * sourceFormat.Channels];
                right[i] = buffer[i * sourceFormat.Channels + 1];
            }
            var leftSingal = new DiscreteSignal(sourceFormat.SampleRate, left);
            var rightSingal = new DiscreteSignal(sourceFormat.SampleRate, right);

            var leftReSample = Operation.Resample(leftSingal, WaveFormat.SampleRate);
            var rightReSample = Operation.Resample(rightSingal, WaveFormat.SampleRate);
            var newLength = leftReSample.Length * WaveFormat.Channels;
            for (int i = 0; i < leftReSample.Length; i++)
            {
                buffer[i * sourceFormat.Channels] = leftReSample[i];
                buffer[i * sourceFormat.Channels + 1] = rightReSample[i];
            }
            //buffer =  newBuffer;
            return newLength;
        }

    }
}
