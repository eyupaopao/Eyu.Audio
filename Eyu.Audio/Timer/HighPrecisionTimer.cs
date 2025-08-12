using System;
using System.Diagnostics;
using System.Threading;

namespace Eyu.Audio.Timer;

/// <summary>
/// 高精度计时器，占用cpu资源较高，可跨平台运行，实现微秒级别的计时。
/// </summary>
public class HighPrecisionTimer : ITimer
{



    private Thread? _thread;
    private volatile bool _running;
    // 微秒级别
    private long _periodMicroseconds;
    private readonly Action _onTick;

    public HighPrecisionTimer(Action onTick)
    {
        _onTick = onTick;
    }

    public void SetPeriod(double milliseconds)
    {
        _periodMicroseconds = (long)(milliseconds * 1000);
    }
    
    public void Start()
    {
        if (_running) return;
        _running = true;
        _thread = new Thread(Run) { IsBackground = true, Priority = ThreadPriority.Highest };
        _thread.Start();
    }

    public void Stop()
    {
        _running = false;
        _thread?.Join();
    }

    private void Run()
    {
        var sw = Stopwatch.StartNew();
        // 每微秒ticks
        long ticksPerMicrosecond = Stopwatch.Frequency / 1_000_000;
        // 等待的ticks
        long intervalTicks = _periodMicroseconds * ticksPerMicrosecond;

        while (_running)
        {
            long startTicks = sw.ElapsedTicks;
            _onTick?.Invoke();

            while (sw.ElapsedTicks - startTicks < intervalTicks)
            {
                Thread.SpinWait(50);
            }
        }
    }

    public void Dispose() => Stop();

}
