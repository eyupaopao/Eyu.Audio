using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NWaves.Operations;
using NWaves.Signals;
using System;

namespace Eyu.Audio.Utils;

public static class AudioEffect
{
    /// <summary>
    /// 将两声道音频的左声道向前移一个相位
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="len"></param>
    /// <param name="patch"></param>
    /// <returns></returns>
    public static byte[] LeftPhaseShift(this byte[] buffer, int len, byte[] patch = null)
    {
        if (len % 4 != 0 || buffer.Length < len) throw new ArgumentException("音频长度不正确");
        var newPatch = new byte[2] { buffer[len - 4], buffer[len - 3] };

        for (int i = len - 8; i >= 0; i -= 4)
        {
            buffer[i + 4] = buffer[i];
            buffer[i + 5] = buffer[i + 1];
        }
        if (patch != null)
        {
            buffer[0] = patch[0];
            buffer[1] = patch[1];
        }
        return newPatch;
    }
    public static byte[] RightPhasePosition(this byte[] buffer, int len, byte[] patch = null)
    {
        if (len % 4 != 0 || buffer.Length < len) throw new ArgumentException("音频长度不正确");
        var newPatch = new byte[2] { buffer[len - 2], buffer[len - 1] };

        for (int i = len - 8; i >= 0; i -= 4)
        {
            buffer[i + 6] = buffer[i + 2];
            buffer[i + 7] = buffer[i + 3];
        }
        if (patch != null)
        {
            buffer[2] = patch[0];
            buffer[3] = patch[1];
        }
        return newPatch;
    }
    public static float[] Wave16ToSample(this byte[] sourceBuffer, int offset, int sourceBufferCount)
    {
        int targetBufferCount = sourceBufferCount / 2;
        float[] targetBuffer = new float[targetBufferCount];
        int outIndex = 0;
        for (int n = offset; n < sourceBufferCount; n += 2)
        {
            targetBuffer[outIndex++] = BitConverter.ToInt16(sourceBuffer, n) / 32768f;
        }
        return targetBuffer;
    }

    //
    // 摘要:
    //     Conversion to 16 bit and clipping
    public unsafe static void Convert32To16(byte[] destBuffer, byte[] source, int sourceCount)
    {

        fixed (byte* ptr = &destBuffer[0])
        {
            fixed (byte* ptr3 = &source[0])
            {
                short* ptr2 = (short*)ptr;
                float* ptr4 = (float*)ptr3;
                int num = sourceCount / 4;
                for (int i = 0; i < num; i++)
                {
                    float num2 = ptr4[i] * 1f;
                    if (num2 > 1f)
                    {
                        ptr2[i] = short.MaxValue;
                    }
                    else if (num2 < -1f)
                    {
                        ptr2[i] = short.MinValue;
                    }
                    else
                    {
                        ptr2[i] = (short)(num2 * 32767f);
                    }
                }
            }
        }
    }



    public static int ReSample(ref float[] sourceSample, int offset, int count, int sourceSampleRate, int destSampleRate)
    {
        if (sourceSample == null || sourceSample.Length < (offset + count)) return 0;
        if (count % 2 != 0) count--;
        var sampleCount = count / 2;
        var left = new float[sampleCount];
        var right = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            left[i] = sourceSample[i * 2];
            right[i] = sourceSample[i * 2 + 1];
        }
        var leftSingal = new DiscreteSignal(sourceSampleRate, left);
        var rightSingal = new DiscreteSignal(sourceSampleRate, right);

        var leftReSample = Operation.Resample(leftSingal, destSampleRate);
        var rightReSample = Operation.Resample(rightSingal, destSampleRate);
        var newLength = leftReSample.Length * 2;
        if (newLength > sourceSample.Length)
        {
            sourceSample = new float[newLength];
        }
        for (int i = 0; i < leftReSample.Length; i++)
        {
            sourceSample[i * 2] = leftReSample[i];
            sourceSample[i * 2 + 1] = rightReSample[i];
        }
        return newLength;
    }

    private static float[] maxSamples;
    private static int sampleCount;
    private static StreamVolumeEventArgs args;
    private static int channels;
    private static int SamplesPerNotification;
    public static Action<StreamVolumeEventArgs> StreamVolume;
    public static void StreamVolumeData(int channels, int SampleRate)
    {
    }
    public static void WaveFormCalculator(float[] sample, int samplesRead)
    {
        if (StreamVolume is not null)
        {
            for (int index = 0; index < samplesRead; index += channels)
            {
                for (int channel = 0; channel < channels; channel++)
                {
                    float sampleValue = Math.Abs(sample[index + channel]);
                    maxSamples[channel] = Math.Max(maxSamples[channel], sampleValue);
                }
                sampleCount++;
                if (sampleCount >= SamplesPerNotification)
                {
                    StreamVolume?.Invoke(args);
                    sampleCount = 0;
                    // n.b. we avoid creating new instances of anything here
                    Array.Clear(maxSamples, 0, channels);
                }
            }
        }


    }



    //单声道复制为双声道
    public static void ChannelCopy(ref byte[] buffer)
    {
        if (channels == 2)
        {
            for (int i = 0; i < buffer.Length; i += 4)
            {
                buffer[i + 2] = buffer[i];
                buffer[i + 3] = buffer[i + 1];
            }
        }
        if (channels == 1)
        {
            byte[] newbuffer = new byte[buffer.Length * 2];
            for (int i = 0; i < buffer.Length; i += 2)
            {
                newbuffer[i * 2] = buffer[i];
                newbuffer[i * 2 + 2] = buffer[i];
                newbuffer[i * 2 + 1] = buffer[i + 1];
                newbuffer[i * 2 + 3] = buffer[i + 1];
            }
            buffer = newbuffer;
        }
    }

    #region pcm音量

    private static short getShort(byte[] src, int start)
    {
        return (short)(src[start] & 0xFF | (src[start + 1] << 8));
    }

    const short SHRT_MAX = 0x7F00;
    const short SHRT_MIN = -0x7F00;

    /**
     * 调节PCM数据音量
     * src  : 
     * nLen :
     * dest : 
     * nBitsPerSample: 16/8
     * multiple: 放大倍数，如1.5
     */
    public static void amplifyPCMData(byte[] src, int nLen, byte[] dest, int nBitsPerSample, float multiple)
    {
        int nCur = 0;
        if (16 == nBitsPerSample)
        {
            while (nCur < nLen)
            {
                short volum = getShort(src, nCur);
                //Log.d(TAG, "volum="+volum);
                volum = (short)(volum * multiple);
                if (volum < SHRT_MIN)
                {
                    volum = SHRT_MIN;
                }
                else if (volum > SHRT_MAX)//爆音的处理   
                {
                    volum = SHRT_MAX;
                }

                dest[nCur] = (byte)(volum & 0xFF);
                dest[nCur + 1] = (byte)((volum >> 8) & 0xFF);
                nCur += 2;
            }

        }
    }

    #endregion
}


public class StreamVolumeHelper
{
    private float[] maxSamples;
    private int sampleCount;
    private StreamVolumeEventArgs args;
    private BufferedWaveProvider waveProvider;
    private SampleChannel sampleChannel;
    private int SamplesPerNotification;
    public Action<StreamVolumeEventArgs> StreamVolume;
    public Action<float[]> SampleStream;
    private WaveFormat _waveFormat;

    public StreamVolumeHelper(WaveFormat waveFormat = null)
    {
        if (waveFormat == null) waveFormat = new WaveFormat(8000, 16, 2);
        _waveFormat = waveFormat;
        Init();
    }
    void Init()
    {
        SamplesPerNotification = _waveFormat.SampleRate / 10;
        maxSamples = new float[_waveFormat.Channels];
        sampleCount = 0;
        args = new StreamVolumeEventArgs() { MaxSampleValues = maxSamples };

        waveProvider = new BufferedWaveProvider(_waveFormat);
        sampleChannel = new SampleChannel(waveProvider);

    }
    public void WaveFormCalculator(byte[] buffer, int count, WaveFormat waveFormat = null)
    {
        if (waveFormat == null) return;
        if (!waveFormat.Equals(_waveFormat))
        {
            _waveFormat = waveFormat;
            Init();
        }
        WaveFormCalculator(buffer, count);
    }

    public void WaveFormCalculator(float[] sample, int samplesRead, WaveFormat waveFormat = null)
    {
        if (!waveFormat.Equals(_waveFormat))
        {
            _waveFormat = waveFormat;
            Init();
        }
        WaveFormCalculator(sample, samplesRead);
    }
    public void WaveFormCalculator(byte[] buffer, int count)
    {
        try
        {
            waveProvider.AddSamples(buffer, 0, count);
            var sample = new float[count / 2];
            var len = sampleChannel.Read(sample, 0, count / 2);
            SampleStream?.Invoke(sample);
            WaveFormCalculator(sample, len);
        }
        catch (Exception ex)
        {

        }
    }

    public void WaveFormCalculator(float[] sample, int samplesRead)
    {
        if (StreamVolume is not null)
        {
            for (int index = 0; index < samplesRead; index += _waveFormat.Channels)
            {
                for (int channel = 0; channel < _waveFormat.Channels; channel++)
                {
                    float sampleValue = Math.Abs(sample[index + channel]);
                    maxSamples[channel] = Math.Max(maxSamples[channel], sampleValue);
                }
                sampleCount++;
                if (sampleCount >= SamplesPerNotification)
                {
                    StreamVolume?.Invoke(args);
                    sampleCount = 0;
                    // n.b. we avoid creating new instances of anything here
                    Array.Clear(maxSamples, 0, _waveFormat.Channels);
                }
            }
        }
    }

}