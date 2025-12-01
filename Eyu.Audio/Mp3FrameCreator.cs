using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Eyu.Audio;

public static class Mp3FrameCreator
{
    static ConstructorInfo? mp3FrameConstructor = typeof(Mp3Frame).GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
  null, Type.EmptyTypes, null);

    /// <summary>
    /// 将一帧mp3数据转换为Mp3Frame对象
    /// </summary>
    /// <param name="buffer"></param>
    /// <returns></returns>
    public static Mp3Frame? CreateMp3Frame(byte[] buffer)
    {
        if (mp3FrameConstructor == null) return null;
        var mp3 = mp3FrameConstructor.Invoke(null) as Mp3Frame;
        if (mp3 == null) return null;
        if (buffer.Length < 4)
        {
            return null;
        }
        byte[] array = buffer[0..4];

        var properties = typeof(Mp3Frame).GetProperties();
        if (!IsValidHeader(array, mp3, properties)) return null;
        foreach (var property in properties)
        {
            if (property.Name == nameof(Mp3Frame.RawData))
            {
                property.SetValue(mp3, buffer);
            }
            if (property.Name == nameof(Mp3Frame.FrameLength))
            {
                property.SetValue(mp3, buffer.Length);
            }
        }
        return mp3;
    }
    private static readonly int[,,] bitRates = new int[2, 3, 15]
    {
        {
            {
                0, 32, 64, 96, 128, 160, 192, 224, 256, 288,
                320, 352, 384, 416, 448
            },
            {
                0, 32, 48, 56, 64, 80, 96, 112, 128, 160,
                192, 224, 256, 320, 384
            },
            {
                0, 32, 40, 48, 56, 64, 80, 96, 112, 128,
                160, 192, 224, 256, 320
            }
        },
        {
            {
                0, 32, 48, 56, 64, 80, 96, 112, 128, 144,
                160, 176, 192, 224, 256
            },
            {
                0, 8, 16, 24, 32, 40, 48, 56, 64, 80,
                96, 112, 128, 144, 160
            },
            {
                0, 8, 16, 24, 32, 40, 48, 56, 64, 80,
                96, 112, 128, 144, 160
            }
        }
    };
    private static readonly int[] sampleRatesVersion1 = new int[3] { 44100, 48000, 32000 };

    private static readonly int[] sampleRatesVersion2 = new int[3] { 22050, 24000, 16000 };

    private static readonly int[] sampleRatesVersion25 = new int[3] { 11025, 12000, 8000 };
    private static readonly int[,] samplesPerFrame = new int[2, 3]
    {
        { 384, 1152, 1152 },
        { 384, 1152, 576 }
    };
    private static bool IsValidHeader(byte[] headerBytes, Mp3Frame frame, IEnumerable<PropertyInfo> properties)
    {
        if (headerBytes[0] == byte.MaxValue && (headerBytes[1] & 0xE0) == 224)
        {
            var property = properties.FirstOrDefault(properties => properties.Name == nameof(Mp3Frame.MpegVersion));
            property?.SetValue(frame, (MpegVersion)((headerBytes[1] & 0x18) >> 3));
            if (frame.MpegVersion == MpegVersion.Reserved)
            {
                return false;
            }

            property = properties.FirstOrDefault(properties => properties.Name == nameof(Mp3Frame.MpegLayer));
            property?.SetValue(frame, (MpegLayer)((headerBytes[1] & 6) >> 1));
            if (frame.MpegLayer == MpegLayer.Reserved)
            {
                return false;
            }
            int num = frame.MpegLayer != MpegLayer.Layer1 ? frame.MpegLayer == MpegLayer.Layer2 ? 1 : 2 : 0;
            property = properties.FirstOrDefault(properties => properties.Name == nameof(Mp3Frame.CrcPresent)); property?.SetValue(frame, (headerBytes[1] & 1) == 0);
            property = properties.FirstOrDefault(properties => properties.Name == nameof(Mp3Frame.BitRateIndex)); property?.SetValue(frame, (headerBytes[2] & 0xF0) >> 4);
            if (frame.BitRateIndex == 15)
            {
                return false;
            }
            int num2 = frame.MpegVersion != MpegVersion.Version1 ? 1 : 0;

            property = properties.FirstOrDefault(properties => properties.Name == nameof(Mp3Frame.BitRate));
            property?.SetValue(frame, bitRates[num2, num, frame.BitRateIndex] * 1000);
            if (frame.BitRate == 0)
            {
                return false;
            }
            int num3 = (headerBytes[2] & 0xC) >> 2;
            if (num3 == 3)
            {
                return false;
            }
            property = properties.FirstOrDefault(properties => properties.Name == nameof(Mp3Frame.SampleRate));
            if (frame.MpegVersion == MpegVersion.Version1)
            {
                property?.SetValue(frame, sampleRatesVersion1[num3]);
            }
            else if (frame.MpegVersion == MpegVersion.Version2)
            {
                property?.SetValue(frame, sampleRatesVersion2[num3]);
            }
            else
            {
                property?.SetValue(frame, sampleRatesVersion25[num3]);
            }
            bool flag = (headerBytes[2] & 2) == 2;
            _ = headerBytes[2];
            property = properties.FirstOrDefault(properties => properties.Name == nameof(Mp3Frame.ChannelMode));
            property?.SetValue(frame, (ChannelMode)((headerBytes[3] & 0xC0) >> 6));
            property = properties.FirstOrDefault(properties => properties.Name == nameof(Mp3Frame.ChannelExtension));
            property?.SetValue(frame, (headerBytes[3] & 0x30) >> 4);
            if (frame.ChannelExtension != 0 && frame.ChannelMode != ChannelMode.JointStereo)
            {
                return false;
            }
            property = properties.FirstOrDefault(properties => properties.Name == nameof(Mp3Frame.Copyright));
            property?.SetValue(frame, (headerBytes[3] & 8) == 8);
            _ = headerBytes[3];
            _ = headerBytes[3];
            int num4 = flag ? 1 : 0;
            property = properties.FirstOrDefault(properties => properties.Name == nameof(Mp3Frame.SampleCount));
            property?.SetValue(frame, samplesPerFrame[num2, num]);
            int num5 = frame.SampleCount / 8;
            property = properties.FirstOrDefault(properties => properties.Name == nameof(Mp3Frame.FrameLength));
            if (frame.MpegLayer == MpegLayer.Layer1)
            {
                property?.SetValue(frame, (num5 * frame.BitRate / frame.SampleRate + num4) * 4);
            }
            else
            {
                property?.SetValue(frame, num5 * frame.BitRate / frame.SampleRate + num4);
            }
            if (frame.FrameLength > 16384)
            {
                return false;
            }
            return true;
        }
        return false;
    }
}
