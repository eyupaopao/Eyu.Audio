using System;
using System.Runtime.InteropServices;

namespace Eyu.Audio.Timer;

/// <summary>
/// 普通计时器,cpu占用低，但精度不高，适用于一般场景
/// </summary>
public class Timer : ITimer, IDisposable
{
    private readonly ITimer timer;

    public Timer(Action onTick)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            timer = new WindowsMediaTimer(onTick);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            timer = new HighPrecisionTimer(onTick);
        }
        else
        {
            timer = new LinuxTimer(onTick);
        }
    }

    /// <summary>
    /// Dispose all resources
    /// </summary>
    public void Dispose()
    {
        timer.Dispose();
    }
    /// <summary>
    /// 间隔事件，单位位毫秒，最小值0.001毫秒，但实际上精度可能大于1毫秒。
    /// </summary>
    /// <param Name="milliseconds"></param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public void SetPeriod(double milliseconds)
    {
        if ((int)(milliseconds * 1000) <= 0)
            milliseconds = 0.001;
        else if ((int)milliseconds > 10000) milliseconds = 10000;
        timer.SetPeriod(milliseconds);
    }
    public void Start()
    {
        timer.Start();
    }

    public void Stop()
    {
        timer.Stop();
    }

}

