using NAudio.Wave;
using NLayer.NAudioSupport;
using System.IO;

namespace Eyu.Audio.Reader;

/// <summary>
/// mp3文件解码
/// </summary>
public class Mp3Reader : Mp3FileReaderBase
{
    /// <summary>Supports opening a MP3 file</summary>
    public Mp3Reader(string mp3FileName)
        : base(File.OpenRead(mp3FileName), CreateFrameDecompressor, true)
    {
    }
    public Mp3Reader(Stream stream) : base(stream, CreateFrameDecompressor, true)
    {

    }
    /// <summary>
    /// 解码器，支持跨平台。
    /// </summary>
    /// <param Name="mp3Format"></param>
    /// <returns></returns>
    public static IMp3FrameDecompressor CreateFrameDecompressor(WaveFormat mp3Format)
    {
        return new Mp3FrameDecompressor(mp3Format);
    }
}
