using NAudio.Utils;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;

namespace Eyu.Audio.Provider;
public class MySampleToWaveProviderL24 : IWaveProvider
{
    private readonly ISampleProvider sourceProvider;
    private readonly WaveFormat waveFormat;
    private volatile float volume;
    private float[] sourceBuffer;
    public Action<byte[], int> AudioBufferHandler;
    public Action<float[], int> SampleBufferHandler;

    public MySampleToWaveProviderL24(ISampleProvider waveProvider)
    {
        if (waveProvider.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
            throw new ArgumentException("Input source provider must be IEEE float", nameof(waveProvider));
        if (waveProvider.WaveFormat.BitsPerSample != 32)
            throw new ArgumentException("Input source provider must be 32 bit", nameof(waveProvider));

        // 创建24位PCM波形格式 (L24)
        waveFormat = new WaveFormat(
            waveProvider.WaveFormat.SampleRate,
            24,
            waveProvider.WaveFormat.Channels);

        this.sourceProvider = waveProvider;
        volume = 1.0f;
        StreamVolumeData();
    }

    public int Read(byte[] destBuffer, int offset, int numBytes)
    {
        // 计算需要的样本数 (24位 = 3字节/样本)
        int samplesRequired = numBytes / 3;
        sourceBuffer = BufferHelpers.Ensure(sourceBuffer, samplesRequired);
        int sourceSamples = sourceProvider.Read(sourceBuffer, 0, samplesRequired);

        int byteIndex = offset;
        for (int sample = 0; sample < sourceSamples; sample++)
        {
            // 应用音量并限制范围
            float sample32 = sourceBuffer[sample] * volume;
            sample32 = Math.Clamp(sample32, -1.0f, 1.0f);

            // 转换为24位整数 (范围: -8388608 到 8388607)
            int int24 = (int)(sample32 * 8388607f);

            // 存储为小端24位格式 (L24)
            // 注意：24位值在3个字节中存储，没有对齐到4字节
            destBuffer[byteIndex++] = (byte)(int24 & 0xFF);             // 最低位字节
            destBuffer[byteIndex++] = (byte)((int24 >> 8) & 0xFF);      // 中间字节
            destBuffer[byteIndex++] = (byte)((int24 >> 16) & 0xFF);     // 最高位字节
        }

        // 触发事件处理
        SampleBufferHandler?.Invoke(sourceBuffer, sourceSamples);
        WaveFormCalculator(sourceBuffer, 0, sourceSamples);
        AudioBufferHandler?.Invoke(destBuffer, sourceSamples * 3);

        return sourceSamples * 3; // 返回实际读取的字节数
    }

    #region 计算波形
    private float[] maxSamples;
    private int sampleCount;
    private int channels;
    private StreamVolumeEventArgs args;

    public void StreamVolumeData()
    {
        channels = WaveFormat.Channels;
        maxSamples = new float[channels];
        SamplesPerNotification = WaveFormat.SampleRate / 10;
        args = new StreamVolumeEventArgs() { MaxSampleValues = maxSamples };
    }

    public int SamplesPerNotification { get; set; }

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
                    Array.Clear(maxSamples, 0, channels);
                }
            }
        }
    }
    #endregion

    public WaveFormat WaveFormat => waveFormat;

    public float Volume
    {
        get => volume;
        set => volume = value;
    }
}
