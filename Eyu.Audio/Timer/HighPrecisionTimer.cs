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

    // 跟踪下一个预期的执行时间点
    private long _nextExpectedTick;

    public HighPrecisionTimer(Action onTick)
    {
        _onTick = onTick ?? throw new ArgumentNullException(nameof(onTick));
    }

    public void SetPeriod(double milliseconds)
    {
        if (milliseconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(milliseconds), "周期必须大于0");
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
    }

    private void Run()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        // 每毫秒tick
        long ticksPerMicrosecond = Stopwatch.Frequency / 1000000;
        // 任务间隔的tick
        long intervalTicks = _periodMicroseconds * ticksPerMicrosecond;
        // 停顿的tick
        int iterations = (int)Math.Max(1, (intervalTicks / ticksPerMicrosecond / 10)); // 确保至少为1

        // 初始化下一个预期时间点
        _nextExpectedTick = stopwatch.ElapsedTicks;

        while (_running)
        {
            // 执行定时任务
            _onTick.Invoke();

            // 计算下一个预期时间点（基于上一个预期时间点 + 周期）
            _nextExpectedTick += intervalTicks;

            // 获取当前实际时间
            long currentTick = stopwatch.ElapsedTicks;

            // 计算需要等待的时间（可能为负，意味着需要立即执行下一次）
            long waitTime = _nextExpectedTick - currentTick;

            // 如果需要等待，执行等待逻辑
            if (waitTime > 0 && _running)
            {
                long waitUntil = currentTick + waitTime;
                while (stopwatch.ElapsedTicks < waitUntil && _running)
                {
                    Thread.SpinWait(iterations);
                }
            }
        }

        stopwatch.Stop();
        _thread?.Join();
    }

    public void Dispose()
    {
        Stop();
        //_thread?.Join();
        GC.SuppressFinalize(this);
    }
}
