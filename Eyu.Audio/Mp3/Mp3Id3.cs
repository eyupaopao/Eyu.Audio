using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eyu.Audio;


public class Mp3Id3
{
    /// <summary>
    // 解析 ID3v2 标签头，以找出 ID3v2 帧的大小。
    /// </summary>
    /// <param name="input">MP3 InputStream</param>
    /// <returns>有标题</returns>
    public static Mp3Id3? ReadID3v2Header(Stream input, bool seek = true)
    {
        byte[] id3header = new byte[3];
        input.ReadExactly(id3header, 0, 3);
        // 查找 ID3v2
        if (id3header[0] == 'I' && id3header[1] == 'D' && id3header[2] == '3')
        {
            var id3 = new Mp3Id3(input);
            if (seek)
                input.Seek(0, SeekOrigin.Begin);
            return id3;
        }
        if (seek)
            input.Seek(0, SeekOrigin.Begin);
        input.Seek(0, SeekOrigin.Begin);
        return null;
    }
    private Mp3Id3(Stream data)
    {
        var temp = new byte[7];
        data.ReadExactly(temp);
        Version = temp[0];
        RVersion = temp[1];
        Flag = temp[2];
        Length = (temp[3] << 21) + (temp[4] << 14) + (temp[5] << 7) + temp[6];
        Decode(data);
    }
    public const string ID3Tag = "ID3";

    public const string TitleTag = "TIT2";
    public const string AutherTag = "TPE1";
    public const string AlbumTag = "TALB";
    public const string TrackTag = "TRCK";
    public const string TimeTag = "TYER";
    public const string TypeTag = "TCON";
    public const string CommentTag = "COMM";
    public const string PictureTag = "APIC";

    public byte Version { get; init; }
    public byte RVersion { get; init; }
    public byte Flag { get; init; }

    public int Length { get; private set; }

    public string? Album { get; private set; }
    public string? Auther { get; private set; }
    public string? Title { get; private set; }
    public byte[]? Image { get; private set; }



    private void Decode(Stream data)
    {
        int pos = 0;
        do
        {
            var temp = new byte[4];
            data.ReadExactly(temp);
            if (temp[0] == 0)
            {
                data.Seek(Length + 10, SeekOrigin.Begin);
                return;
            }
            pos += 4;
            string tag = Encoding.UTF8.GetString(temp);
            data.ReadExactly(temp);
            pos += 4;
            int size = temp[0] << 24 | temp[1] << 16 | temp[2] << 8 | temp[3];
            data.ReadExactly(temp, 0, 2);
            pos += 2;
            //int flag = temp[0] << 8 | temp[1];
            switch (tag)
            {
                case TitleTag:
                    Title = ReadString(data, size);
                    break;
                case AutherTag:
                    Auther = ReadString(data, size);
                    break;
                case AlbumTag:
                    Album = ReadString(data, size);
                    break;
                case PictureTag:
                    int encoding = data.ReadByte();
                    string mimeType = ReadNullTerminatedString(data, encoding);
                    //int pictureType = data.ReadByte();
                    string description = ReadNullTerminatedString(data, encoding);
                    int imageSize = size - (1 + mimeType.Length + 1 + 1 + description.Length + 1);
                    Image = new byte[imageSize];
                    data.ReadExactly(Image);
                    break;
                default:
                    data.Seek(size, SeekOrigin.Current);
                    break;
            }
            pos += size;
        }
        while (pos < Length);
    }

    private static string ReadNullTerminatedString(Stream data, int encoding)
    {
        var result = new List<byte>();
        int b;
        while ((b = data.ReadByte()) != 0)
        {
            if (b == -1) break;
            result.Add((byte)b);
        }
        var temp = result.ToArray();
        switch (encoding)
        {
            //iso-8859-1
            case 0:
                return Encoding.Latin1.GetString(temp);
            //UTF-16
            case 1:
                if (temp[0] == 0xFE && temp[1] == 0xFF)
                {
                    return Encoding.BigEndianUnicode.GetString(temp, 2, temp.Length - 2);
                }
                else
                {
                    return Encoding.Unicode.GetString(temp, 2, temp.Length - 2);
                }
            //UTF-8
            case 3:
                return Encoding.UTF8.GetString(temp);
        }

        return "";
    }

    private static string? ReadString(Stream data, int size)
    {
        var type = new byte[1];
        data.ReadExactly(type);
        var str = new byte[size - 1];
        data.ReadExactly(str);
        switch (type[0])
        {
            //iso-8859-1
            case 0:
                return Encoding.Latin1.GetString(str);
            //UTF-16
            case 1:
                if (str[0] == 0xFE && str[1] == 0xFF)
                {
                    return Encoding.BigEndianUnicode.GetString(str, 2, str.Length - 2);
                }
                else
                {
                    return Encoding.Unicode.GetString(str, 2, str.Length - 2);
                }
            //UTF-8
            case 3:
                return Encoding.UTF8.GetString(str);
        }

        return null;
    }
}
