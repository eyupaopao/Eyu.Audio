using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eyu.Audio.Utils;

public static class BufferExtension
{
    public static int readInt8(this byte[] buffer, int offset)
    {
        return buffer[offset];
    }
    public static int ReadInt16BE(this byte[] buffer, int offset)
    {
        var r1 = buffer[offset + 0] << 8;
        var r2 = buffer[offset + 1] << 0;
        return r1 + r2;
    }
    /// <summary>
    /// 高位在前
    /// </summary>
    /// <param Name="buffer"></param>
    /// <param Name="value"></param>
    /// <param Name="offset"></param>
    public static void WriteUInt16BE(this byte[] buffer, ushort value, int offset)
    {
        buffer[offset] = (byte)(value >> 8);
        buffer[offset + 1] = (byte)(value & 0xFF);
    }
    /// <summary>
    /// 高位在后
    /// </summary>
    /// <param Name="buffer"></param>
    /// <param Name="value"></param>
    /// <param Name="offset"></param>
    public static void WriteUInt16LE(this byte[] buffer, ushort value, int offset)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)(value >> 8);
    }

    public static void WriteUInt32BE(this byte[] buffer, uint value, int offset)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }
    public static void WriteUInt32LE(this byte[] buffer, uint value, int offset)
    {
        buffer[offset + 3] = (byte)(value >> 24);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset] = (byte)value;
    }
    public static int ReadInt16LE(this byte[] buffer, int offset)
    {
        var r1 = buffer[offset + 1] << 8;
        var r2 = buffer[offset + 0] << 0;
        return r1 + r2;
    }
    public static long ReadLongBE(this byte[] buffer, int offset)
    {
        var r1 = buffer[offset] << 24;
        var r2 = buffer[offset + 1] << 16;
        var r3 = buffer[offset + 2] << 8;
        var r4 = buffer[offset + 3] << 0;
        var res = r1 + r2 + r3 + r4;
        return res;
    }

    public static long ReadLongLE(this byte[] buffer, int offset)
    {
        var r1 = buffer[offset + 3] << 24;
        var r2 = buffer[offset + 2] << 16;
        var r3 = buffer[offset + 1] << 8;
        var r4 = buffer[offset + 0] << 0;
        return r1 + r2 + r3 + r4;
    }

    public static TimeSpan GetTimestampFromUnixEpoch(this DateTime dateTime)
    {
        return dateTime - DateTime.UnixEpoch;
    }
}
