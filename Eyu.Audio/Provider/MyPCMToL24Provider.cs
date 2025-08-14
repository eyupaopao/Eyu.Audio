using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eyu.Audio.Provider;
public class MyPCMToL24Provider : IWaveProvider
{
    private readonly IWaveProvider sourceProvider;
    private readonly WaveFormat inputFormat;
    private byte[] sourceBuffer;

    public MyPCMToL24Provider(IWaveProvider waveProvider)
    {
        this.sourceProvider = waveProvider;
        inputFormat = waveProvider.WaveFormat;

        // 检查输入格式是否支持（16位或32位PCM）
        if (inputFormat.BitsPerSample != 16 && inputFormat.BitsPerSample != 32)
        {
            throw new ArgumentException("仅支持16位或32位PCM输入格式", nameof(waveProvider));
        }

        WaveFormat = new WaveFormat(
            inputFormat.SampleRate,
            24,
            inputFormat.Channels);
    }

    public WaveFormat WaveFormat { get; }

    public int Read(byte[] buffer, int offset, int count)
    {
        // 计算需要的源样本数（24位输出的每个样本占3字节）
        int samplesNeeded = count / 3;
        int sourceBytesNeeded;

        // 根据输入格式计算所需的源字节数
        if (inputFormat.BitsPerSample == 16)
        {
            sourceBytesNeeded = samplesNeeded * 2; // 16位样本占2字节
        }
        else // 32位
        {
            sourceBytesNeeded = samplesNeeded * 4; // 32位样本占4字节
        }

        // 确保源缓冲区足够大
        if (sourceBuffer == null || sourceBuffer.Length < sourceBytesNeeded)
        {
            sourceBuffer = new byte[sourceBytesNeeded];
        }

        // 从源提供器读取数据
        int sourceBytesRead = sourceProvider.Read(sourceBuffer, 0, sourceBytesNeeded);
        if (sourceBytesRead == 0)
        {
            return 0; // 没有更多数据
        }

        // 计算实际读取的样本数和输出字节数
        int samplesRead = inputFormat.BitsPerSample == 16
            ? sourceBytesRead / 2
            : sourceBytesRead / 4;
        int outputBytes = samplesRead * 3;

        // 转换每个样本到24位
        int bufferIndex = offset;
        for (int i = 0; i < samplesRead; i++)
        {
            int sampleValue;

            if (inputFormat.BitsPerSample == 16)
            {
                // 读取16位样本（小端格式）
                sampleValue = BitConverter.ToInt16(sourceBuffer, i * 2);
                // 转换为24位（左移8位以扩展范围）
                sampleValue <<= 8;
            }
            else
            {
                // 读取32位样本（小端格式）
                sampleValue = BitConverter.ToInt32(sourceBuffer, i * 4);
                // 转换为24位（右移8位以适应范围）
                sampleValue >>= 8;
            }

            // 将24位样本拆分为3个字节（小端格式）
            buffer[bufferIndex++] = (byte)(sampleValue & 0xFF);
            buffer[bufferIndex++] = (byte)((sampleValue >> 8) & 0xFF);
            buffer[bufferIndex++] = (byte)((sampleValue >> 16) & 0xFF);
        }

        return outputBytes;
    }
}
