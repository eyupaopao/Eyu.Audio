using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Eyu.Audio.Timer;

internal class WindowsMediaTimer : ITimer
{
    #region api
    private delegate void TimerCallback(int id, int msg, int user, int param1, int param2);

    [DllImport("winmm.dll", EntryPoint = "timeSetEvent")]
    private static extern int TimeSetEvent(int delay, int resolution, TimerCallback callback, int user, int eventType);

    [DllImport("winmm.dll", EntryPoint = "timeKillEvent")]
    private static extern int TimeKillEvent(int id);

    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static extern int TimeBeginPeriod(int uPeriod);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static extern int TimeEndPeriod(int uPeriod);
    #endregion

    private int _timerId;
    private TimerCallback _callback;
    private double _periodMs;
    private bool _running;
    private readonly Action _onTick;

    public WindowsMediaTimer(Action onTick)
    {
        _onTick = onTick;
        _callback = new TimerCallback(TimerProc);
    }

    public void SetPeriod(double milliseconds)
    {
        if (milliseconds < 1.0) milliseconds = 1;
        else if (milliseconds > 10000) milliseconds = 10000;
        _periodMs = milliseconds;
    }

    public void Start()
    {
        if (_running) return;

        int period = (int)Math.Round(_periodMs);
        TimeBeginPeriod(period); // 提高系统时钟精度

        _timerId = TimeSetEvent(
            delay: period,
            resolution: period,
            callback: _callback,
            user: 0,
            eventType: 0x0001 | 0x0100 // TIME_PERIODIC | TIME_KILL_SYNCHRONOUS
        );

        if (_timerId == 0)
            throw new InvalidOperationException("Failed to start multimedia timer.");

        _running = true;
    }

    public void Stop()
    {
        if (!_running) return;

        TimeKillEvent(_timerId);
        TimeEndPeriod((int)Math.Round(_periodMs));
        _running = false;
    }

    private void TimerProc(int id, int msg, int user, int param1, int param2)
    {
        _onTick?.Invoke();
    }

    public void Dispose()
    {
        Stop();
    }
}
