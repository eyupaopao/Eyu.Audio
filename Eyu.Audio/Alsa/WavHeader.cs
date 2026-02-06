using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace Eyu.Audio.Alsa;

/// <summary>
/// 表示 WAV 音频文件格式的文件头结构。
/// 包含音频文件的元数据信息，如采样率、声道数、位深度等。
/// </summary>
struct WavHeader
{
    /// <summary>
    /// RIFF 块标识符，固定为 "RIFF"（4 字节）。
    /// </summary>
    public char[] ChunkId { get; set; }

    /// <summary>
    /// 文件大小减去 8 字节（ChunkId 和 ChunkSize 本身的大小）。
    /// </summary>
    public uint ChunkSize { get; set; }

    /// <summary>
    /// 文件格式标识符，固定为 "WAVE"（4 字节）。
    /// </summary>
    public char[] Format { get; set; }

    /// <summary>
    /// 格式子块标识符，固定为 "fmt "（4 字节，包含空格）。
    /// </summary>
    public char[] Subchunk1Id { get; set; }

    /// <summary>
    /// 格式子块的大小，对于 PCM 格式通常为 16 字节。
    /// </summary>
    public uint Subchunk1Size { get; set; }

    /// <summary>
    /// 音频格式代码。1 表示 PCM（脉冲编码调制），其他值表示压缩格式。
    /// </summary>
    public ushort AudioFormat { get; set; }

    /// <summary>
    /// 声道数。1 表示单声道，2 表示立体声。
    /// </summary>
    public ushort NumChannels { get; set; }

    /// <summary>
    /// 采样率，单位为 Hz（每秒采样次数）。常见值如 44100、48000 等。
    /// </summary>
    public uint SampleRate { get; set; }

    /// <summary>
    /// 字节率，表示每秒的字节数。计算公式：SampleRate × NumChannels × BitsPerSample / 8。
    /// </summary>
    public uint ByteRate { get; set; }

    /// <summary>
    /// 块对齐，表示一个采样帧的字节数。计算公式：NumChannels × BitsPerSample / 8。
    /// </summary>
    public ushort BlockAlign { get; set; }

    /// <summary>
    /// 每个采样点的位数（位深度）。常见值如 8、16、24、32。
    /// </summary>
    public ushort BitsPerSample { get; set; }

    /// <summary>
    /// 数据子块标识符，固定为 "data"（4 字节）。
    /// </summary>
    public char[] Subchunk2Id { get; set; }

    /// <summary>
    /// 数据子块的大小，表示音频数据的字节数。
    /// </summary>
    public uint Subchunk2Size { get; set; }

    /// <summary>
    /// 将 WAV 文件头写入指定的流中。
    /// </summary>
    /// <param name="wavStream">要写入的目标流。</param>
    /// <exception cref="WavFormatException">当写入过程中发生错误时抛出。</exception>
    public void WriteToStream(Stream wavStream)
    {
        Span<byte> writeBuffer2 = stackalloc byte[2];
        Span<byte> writeBuffer4 = stackalloc byte[4];

        try
        {
            Encoding.ASCII.GetBytes(ChunkId, writeBuffer4);
            wavStream.Write(writeBuffer4);

            BinaryPrimitives.WriteUInt32LittleEndian(writeBuffer4, ChunkSize);
            wavStream.Write(writeBuffer4);

            Encoding.ASCII.GetBytes(Format, writeBuffer4);
            wavStream.Write(writeBuffer4);

            Encoding.ASCII.GetBytes(Subchunk1Id, writeBuffer4);
            wavStream.Write(writeBuffer4);

            BinaryPrimitives.WriteUInt32LittleEndian(writeBuffer4, Subchunk1Size);
            wavStream.Write(writeBuffer4);

            BinaryPrimitives.WriteUInt16LittleEndian(writeBuffer2, AudioFormat);
            wavStream.Write(writeBuffer2);

            BinaryPrimitives.WriteUInt16LittleEndian(writeBuffer2, NumChannels);
            wavStream.Write(writeBuffer2);

            BinaryPrimitives.WriteUInt32LittleEndian(writeBuffer4, SampleRate);
            wavStream.Write(writeBuffer4);

            BinaryPrimitives.WriteUInt32LittleEndian(writeBuffer4, ByteRate);
            wavStream.Write(writeBuffer4);

            BinaryPrimitives.WriteUInt16LittleEndian(writeBuffer2, BlockAlign);
            wavStream.Write(writeBuffer2);

            BinaryPrimitives.WriteUInt16LittleEndian(writeBuffer2, BitsPerSample);
            wavStream.Write(writeBuffer2);

            Encoding.ASCII.GetBytes(Subchunk2Id, writeBuffer4);
            wavStream.Write(writeBuffer4);

            BinaryPrimitives.WriteUInt32LittleEndian(writeBuffer4, Subchunk2Size);
            wavStream.Write(writeBuffer4);
        }
        catch (Exception ex)
        {
            throw new WavFormatException(ExceptionMessages.UnableToWriteWavHeader, ex);
        }
    }

    /// <summary>
    /// 根据指定的音频参数构建 WAV 文件头。
    /// </summary>
    /// <param name="sampleRate">采样率，单位为 Hz。</param>
    /// <param name="channels">声道数（1 = 单声道，2 = 立体声）。</param>
    /// <param name="bitsPerSample">每个采样点的位数（位深度）。</param>
    /// <returns>构建好的 WAV 文件头实例。</returns>
    public static WavHeader Build(uint sampleRate, ushort channels, ushort bitsPerSample)
    {
        return new WavHeader
        {
            ChunkId = new[] { 'R', 'I', 'F', 'F' },
            ChunkSize = 0xFFFFFFFF,
            Format = new[] { 'W', 'A', 'V', 'E' },
            Subchunk1Id = new[] { 'f', 'm', 't', ' ' },
            Subchunk1Size = 16,
            AudioFormat = 1,
            NumChannels = channels,
            SampleRate = sampleRate,
            ByteRate = sampleRate * bitsPerSample * channels / 8,
            BlockAlign = (ushort)(bitsPerSample * channels / 8),
            BitsPerSample = bitsPerSample,
            Subchunk2Id = new[] { 'd', 'a', 't', 'a' },
            Subchunk2Size = 0xFFFFFFFF
        };
    }

    /// <summary>
    /// 从指定的流中读取并解析 WAV 文件头。
    /// </summary>
    /// <param name="wavStream">包含 WAV 文件头的输入流。</param>
    /// <returns>解析得到的 WAV 文件头实例。</returns>
    /// <exception cref="WavFormatException">当读取或解析过程中发生错误时抛出。</exception>
    public static WavHeader FromStream(Stream wavStream)
    {
        Span<byte> readBuffer2 = stackalloc byte[2];
        Span<byte> readBuffer4 = stackalloc byte[4];

        var header = new WavHeader();

        try
        {
            wavStream.Read(readBuffer4);
            header.ChunkId = Encoding.ASCII.GetString(readBuffer4).ToCharArray();

            wavStream.Read(readBuffer4);
            header.ChunkSize = BinaryPrimitives.ReadUInt32LittleEndian(readBuffer4);

            wavStream.Read(readBuffer4);
            header.Format = Encoding.ASCII.GetString(readBuffer4).ToCharArray();

            wavStream.Read(readBuffer4);
            header.Subchunk1Id = Encoding.ASCII.GetString(readBuffer4).ToCharArray();

            wavStream.Read(readBuffer4);
            header.Subchunk1Size = BinaryPrimitives.ReadUInt32LittleEndian(readBuffer4);

            wavStream.Read(readBuffer2);
            header.AudioFormat = BinaryPrimitives.ReadUInt16LittleEndian(readBuffer2);

            wavStream.Read(readBuffer2);
            header.NumChannels = BinaryPrimitives.ReadUInt16LittleEndian(readBuffer2);

            wavStream.Read(readBuffer4);
            header.SampleRate = BinaryPrimitives.ReadUInt32LittleEndian(readBuffer4);

            wavStream.Read(readBuffer4);
            header.ByteRate = BinaryPrimitives.ReadUInt32LittleEndian(readBuffer4);

            wavStream.Read(readBuffer2);
            header.BlockAlign = BinaryPrimitives.ReadUInt16LittleEndian(readBuffer2);

            wavStream.Read(readBuffer2);
            header.BitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(readBuffer2);

            wavStream.Read(readBuffer4);
            header.Subchunk2Id = Encoding.ASCII.GetString(readBuffer4).ToCharArray();

            wavStream.Read(readBuffer4);
            header.Subchunk2Size = BinaryPrimitives.ReadUInt32LittleEndian(readBuffer4);
        }
        catch (Exception exception)
        {
            throw new WavFormatException(ExceptionMessages.UnableToReadWavHeader, exception);
        }

        return header;
    }

    /// <summary>
    /// 将 WAV 文件头转换为字节数组。
    /// </summary>
    /// <returns>包含 WAV 文件头数据的字节数组（44 字节）。</returns>
    /// <exception cref="WavFormatException">当转换过程中发生错误时抛出。</exception>
    public byte[] ToBytes()
    {
        using var memoryStream = new MemoryStream(44);
        WriteToStream(memoryStream);
        return memoryStream.ToArray();
    }

    /// <summary>
    /// 从字节数组创建 WAV 文件头。
    /// </summary>
    /// <param name="bytes">包含 WAV 文件头数据的字节数组（至少 44 字节）。</param>
    /// <returns>解析得到的 WAV 文件头实例。</returns>
    /// <exception cref="ArgumentException">当字节数组长度不足 44 字节时抛出。</exception>
    /// <exception cref="WavFormatException">当解析过程中发生错误时抛出。</exception>
    public static WavHeader FromBytes(byte[] bytes)
    {
        if (bytes == null)
        {
            throw new ArgumentNullException(nameof(bytes));
        }

        if (bytes.Length < 44)
        {
            throw new ArgumentException("字节数组长度必须至少为 44 字节", nameof(bytes));
        }

        using var memoryStream = new MemoryStream(bytes);
        return FromStream(memoryStream);
    }
}
