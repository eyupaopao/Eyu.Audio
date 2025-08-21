using System;
using System.Diagnostics;
using System.Threading;

namespace Eyu.Audio.Timer;

/// <summary>
/// 高精度计时器，占用cpu资源较高，可跨平台运行，实现微秒级别的计时。
/// </summary>
public class HighPrecisionTimer : ITimer, IDisposable
{
    private Thread? _thread;

    private volatile bool _running;

    private long _periodMicroseconds;

    private readonly Action _onTick;

    public HighPrecisionTimer(Action onTick)
    {
        _onTick = onTick;
    }

    public void SetPeriod(double milliseconds)
    {
        _periodMicroseconds = (long)(milliseconds * 1000.0);
    }

    public void Start()
    {
        if (!_running)
        {
            _running = true;
            _thread = new Thread(Run)
            {
                IsBackground = true,
                Priority = ThreadPriority.Highest
            };
            _thread.Start();
        }
    }

    public void Stop()
    {
        _running = false;
        _thread?.Join();
    }

    private void Run()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        long ticksPerMicrosecond = Stopwatch.Frequency / 1000000;
        long intervalTicks = _periodMicroseconds * ticksPerMicrosecond;
        while (_running)
        {
            long elapsedTicks = stopwatch.ElapsedTicks;
            _onTick?.Invoke();
            while (stopwatch.ElapsedTicks - elapsedTicks < intervalTicks)
            {
                Thread.SpinWait(1);
            }
        }
    }

    public void Dispose()
    {
        Stop();
    }
}