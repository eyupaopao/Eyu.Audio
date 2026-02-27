using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
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

    // 绑定的 CPU 核心索引，-1 表示不绑定
    private int _cpuAffinity = -1;

    public HighPrecisionTimer(Action onTick)
    {
        _onTick = onTick ?? throw new ArgumentNullException(nameof(onTick));
    }

    /// <summary>
    /// 设置定时器线程绑定的 CPU 核心（从 0 开始）。-1 表示不绑定。
    /// 必须在 Start() 之前调用。
    /// </summary>
    public void SetCpuAffinity(int coreIndex)
    {
        _cpuAffinity = coreIndex;
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
        // Windows 上绑定 CPU 核心，减少调度抖动
        if (_cpuAffinity >= 0 && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                var proc = System.Diagnostics.Process.GetCurrentProcess();
                foreach (ProcessThread pt in proc.Threads)
                {
                    if (pt.Id == Environment.CurrentManagedThreadId)
                    {
                        pt.ProcessorAffinity = (nint)(1L << _cpuAffinity);
                        pt.PriorityLevel = ThreadPriorityLevel.TimeCritical;
                        break;
                    }
                }
            }
            catch { }
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
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
