using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eyu.Audio;

internal class PTPTimmer
{
    private static readonly Stopwatch stopwatch = Stopwatch.StartNew();
    private static readonly long initialTicks = DateTime.UtcNow.Ticks;
    private static readonly double _ticksToNanoseconds = 1000000000.0 / Stopwatch.Frequency;
    /// <summary>
    /// utc时间；从 0001 年 1 月 1 日开始计时
    /// </summary>
    public static long UtcNowNanoseconds
    {
        get { return initialTicks * 100 + (long)(stopwatch.ElapsedTicks * _ticksToNanoseconds); }
    }
    /// <summary>
    /// 时间戳；Unix 纪元是 1970 年 1 月 1 日
    /// </summary>
    public static long TimeStampNanoseconds
    {
        get
        {
            return UtcNowNanoseconds - DateTime.UnixEpoch.Ticks * 100;
        }
    }

    /// <summary>
    /// 开始运行到现在的时间
    /// </summary>
    public static long TotalNanoseconds
    {
        get
        {
            return (long)(stopwatch.ElapsedTicks * _ticksToNanoseconds);
        }
    }
    public static byte[] GetTimestamp()
    {
        long seconds = TimeStampNanoseconds / 1000_000;
        long nanoseconds = TimeStampNanoseconds % 1000_1000;

        var timestamp = new byte[10];
        timestamp[0] = (byte)(seconds >> 32);
        timestamp[1] = (byte)(seconds >> 24);
        timestamp[2] = (byte)(seconds >> 16);
        timestamp[3] = (byte)(seconds >> 8);
        timestamp[4] = (byte)(seconds & 0xFF);
        timestamp[5] = (byte)(nanoseconds >> 24);
        timestamp[6] = (byte)(nanoseconds >> 16);
        timestamp[7] = (byte)(nanoseconds >> 8);
        timestamp[8] = (byte)(nanoseconds & 0xFF);
        timestamp[9] = 0; // Reserved

        return timestamp;
    }
}
