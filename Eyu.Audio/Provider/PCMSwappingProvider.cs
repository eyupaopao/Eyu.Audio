using NAudio.Wave;
using System;

namespace Eyu.Audio.Provider;

/// <summary>
/// 通用 PCM 大小端转换 Provider。从源 IWaveProvider 读取 PCM 数据并交换每个样本的字节序。
/// </summary>
public class PCMSwappingProvider : IWaveProvider
{
    private readonly IWaveProvider sourceProvider;
    private readonly int bytesPerSample;

    /// <summary>
    /// 构造 PCM 大小端转换 Provider。
    /// </summary>
    /// <param name="sourceProvider">源 Provider，须为 PCM，支持 16/24/32 位</param>
    public PCMSwappingProvider(IWaveProvider sourceProvider)
    {
        this.sourceProvider = sourceProvider ?? throw new ArgumentNullException(nameof(sourceProvider));

        if (sourceProvider.WaveFormat.Encoding != WaveFormatEncoding.Pcm)
            throw new ArgumentException("需要 PCM 输入", nameof(sourceProvider));

        int bits = sourceProvider.WaveFormat.BitsPerSample;
        if (bits != 16 && bits != 24 && bits != 32)
            throw new ArgumentException("仅支持 16、24 或 32 位 PCM", nameof(sourceProvider));

        bytesPerSample = bits / 8;
    }

    /// <summary>
    /// 当前 WaveFormat（与源相同）。
    /// </summary>
    public WaveFormat WaveFormat => sourceProvider.WaveFormat;

    /// <summary>
    /// 从源读取数据并交换每个样本的字节序后写入 buffer。
    /// </summary>
    public int Read(byte[] buffer, int offset, int count)
    {
        int bytesRead = sourceProvider.Read(buffer, offset, count);
        if (bytesRead <= 0) return bytesRead;

        // 按样本对齐，避免在样本中间截断
        int sampleCount = bytesRead / bytesPerSample;
        int alignedBytes = sampleCount * bytesPerSample;

        for (int i = 0; i < alignedBytes; i += bytesPerSample)
        {
            SwapSample(buffer, offset + i, bytesPerSample);
        }

        return alignedBytes;
    }

    private static void SwapSample(byte[] buffer, int offset, int bytesPerSample)
    {
        switch (bytesPerSample)
        {
            case 2: // 16-bit
                (buffer[offset], buffer[offset + 1]) = (buffer[offset + 1], buffer[offset]);
                break;
            case 3: // 24-bit
                (buffer[offset], buffer[offset + 2]) = (buffer[offset + 2], buffer[offset]);
                break;
            case 4: // 32-bit
                (buffer[offset], buffer[offset + 3]) = (buffer[offset + 3], buffer[offset]);
                (buffer[offset + 1], buffer[offset + 2]) = (buffer[offset + 2], buffer[offset + 1]);
                break;
        }
    }
}
