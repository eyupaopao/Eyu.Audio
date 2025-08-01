using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Eyu.Audio.Timer;

/// <summary>
/// linux平台的高精度定时器，在低性能硬件中可能会因为系统调度而导致延迟，如果需要更高的精度和实时性能，建议使用HighPrecisionTimer。
/// </summary>
internal class LinuxTimer : ITimer, IDisposable
{
    private Thread? _thread;
    private volatile bool _running;
    private long _periodMicroseconds;
    private readonly Action _onTick;

    public LinuxTimer(Action onTick)
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
        _thread = new Thread(Run) { IsBackground = true };
        _thread.Start();
    }

    public void Stop()
    {
        _running = false;
        _thread?.Join();
    }

    private void Run()
    {
        while (_running)
        {
            _onTick?.Invoke();
            NanoSleep(_periodMicroseconds);
        }
    }

    private void NanoSleep(long microseconds)
    {
        var ts = new timespec
        {
            tv_sec = microseconds / 1_000_000,
            tv_nsec = (microseconds % 1_000_000) * 1000
        };
        clock_nanosleep(1, 0, ref ts, IntPtr.Zero);
    }

    [DllImport("libc.so.6")]
    private static extern int clock_nanosleep(int clock_id, int flags, ref timespec req, IntPtr rem);

    [StructLayout(LayoutKind.Sequential)]
    public struct timespec
    {
        public long tv_sec;
        public long tv_nsec;
    }

    public void Dispose() => Stop();
}
