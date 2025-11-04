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
    #region read

    #region be
    public static short ReadInt16BE(this byte[] buffer, int offset)
    {
        var r1 = buffer[offset + 0] << 8;
        var r2 = buffer[offset + 1] << 0;
        return (short)(r1 + r2);
    }
    public static ushort ReadUInt16BE(this byte[] buffer, int offset)
    {
        return (ushort)((buffer[offset] << 8) | buffer[offset + 1]);
    }

    public static uint ReadUInt32BE(this byte[] buffer, int offset)
    {
        return (uint)(
            (buffer[offset] << 24) |
            (buffer[offset + 1] << 16) |
            (buffer[offset + 2] << 8) |
            buffer[offset + 3]
        );
    }

    /// <summary>
    /// 从字节数组读取大端序64位有符号整数（long）
    /// </summary>
    /// <param name="buffer">待读取的字节数组</param>
    /// <param name="offset">起始偏移量（从该位置开始读取8个字节）</param>
    /// <returns>大端序解析后的long值</returns>
    /// <exception cref="ArgumentNullException">buffer为null</exception>
    /// <exception cref="ArgumentOutOfRangeException">偏移量超出缓冲区范围（不足8个字节）</exception>
    public static long ReadLongBE(this byte[] buffer, int offset)
    {
        // 边界校验：避免空引用和数组越界
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer), "字节数组不能为null");
        if (offset < 0 || offset + 7 >= buffer.Length)
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                $"偏移量{offset}无效，需确保从偏移量开始至少有8个字节（缓冲区长度：{buffer.Length}）"
            );

        // 大端序拼接：按字节顺序从高位到低位拼接（共8个字节）
        // 每个字节先转为long（避免移位时溢出），再左移对应位数，最后通过 | 合并
        return (long)buffer[offset] << 56       // 第0字节：最高位（63-56位）
             | (long)buffer[offset + 1] << 48  // 第1字节（55-48位）
             | (long)buffer[offset + 2] << 40  // 第2字节（47-40位）
             | (long)buffer[offset + 3] << 32  // 第3字节（39-32位）
             | (long)buffer[offset + 4] << 24  // 第4字节（31-24位）
             | (long)buffer[offset + 5] << 16  // 第5字节（23-16位）
             | (long)buffer[offset + 6] << 8   // 第6字节（15-8位）
             | buffer[offset + 7];             // 第7字节：最低位（7-0位）
    }

    /// <summary>
    /// 从字节数组读取大端序64位无符号整数（ulong）
    /// </summary>
    /// <param name="buffer">待读取的字节数组</param>
    /// <param name="offset">起始偏移量（从该位置开始读取8个字节）</param>
    /// <returns>大端序解析后的ulong值</returns>
    /// <exception cref="ArgumentNullException">buffer为null</exception>
    /// <exception cref="ArgumentOutOfRangeException">偏移量超出缓冲区范围（不足8个字节）</exception>
    public static ulong ReadULongBE(this byte[] buffer, int offset)
    {
        // 边界校验（同ReadLongBE）
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer), "字节数组不能为null");
        if (offset < 0 || offset + 7 >= buffer.Length)
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                $"偏移量{offset}无效，需确保从偏移量开始至少有8个字节（缓冲区长度：{buffer.Length}）"
            );

        // 大端序拼接：ulong为无符号类型，拼接逻辑与long类似，但无需考虑符号位
        return (ulong)buffer[offset] << 56       // 第0字节：最高位（63-56位）
             | (ulong)buffer[offset + 1] << 48  // 第1字节（55-48位）
             | (ulong)buffer[offset + 2] << 40  // 第2字节（47-40位）
             | (ulong)buffer[offset + 3] << 32  // 第3字节（39-32位）
             | (ulong)buffer[offset + 4] << 24  // 第4字节（31-24位）
             | (ulong)buffer[offset + 5] << 16  // 第5字节（23-16位）
             | (ulong)buffer[offset + 6] << 8   // 第6字节（15-8位）
             | buffer[offset + 7];             // 第7字节：最低位（7-0位）
    }

    #endregion
    #region le

    public static int ReadInt16LE(this byte[] buffer, int offset)
    {
        var r1 = buffer[offset + 1] << 8;
        var r2 = buffer[offset + 0] << 0;
        return r1 + r2;
    }

    public static long ReadLongLE(this byte[] buffer, int offset)
    {
        var r1 = buffer[offset + 3] << 24;
        var r2 = buffer[offset + 2] << 16;
        var r3 = buffer[offset + 1] << 8;
        var r4 = buffer[offset + 0] << 0;
        return r1 + r2 + r3 + r4;
    }

    #endregion

    #endregion
    #region write
    public static void WriteInt16BE(this byte[] buffer, uint value, int offset)
    {
        buffer[offset] = (byte)(value >> 8);
        buffer[offset + 1] = (byte)(value & 0xFF);
    }
    public static void WriteInt16LE(this byte[] buffer, uint value, int offset)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)(value >> 8);
    }

    #region be

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

    public static void WriteUInt32BE(this byte[] buffer, uint value, int offset)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }
    #endregion
    #region le

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
    public static void WriteUInt32LE(this byte[] buffer, uint value, int offset)
    {
        buffer[offset + 3] = (byte)(value >> 24);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset] = (byte)value;
    }

    #endregion
    #endregion


    public static TimeSpan GetTimestampFromUnixEpoch(this DateTime dateTime)
    {
        return dateTime - DateTime.UnixEpoch;
    }
}
