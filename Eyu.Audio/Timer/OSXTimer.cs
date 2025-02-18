using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Eyu.Audio.Timer;


internal class OSXTimer : ITimer
{
    static OSXTimer()
    {
        queue = InteropOSX.DispatchQueueCreate("osx timer", 0);
    }

    private IntPtr timer;
    private static IntPtr queue;
    private Action _tick;
    private bool running;
    public OSXTimer(Action tick)
    {
        _tick = tick;

        if (queue == IntPtr.Zero)
        {
            throw new Exception($"Unable to create dispatch queue, errno = {Marshal.GetLastPInvokeError()}");
        }
        // 创建调度源
        timer = InteropOSX.DispatchSourceCreate(InteropOSX.DISPATCH_SOURCE_TYPE_TIMER, 0, 0, queue);
        if (timer == IntPtr.Zero)
        {
            throw new Exception($"Unable to create dispatch source, errno = {Marshal.GetLastPInvokeError()}");
        }

        // 设置事件处理程序
        InteropOSX.DispatchSourceSetEventHandler(timer, Scheduler);
        ulong start = InteropOSX.DispatchTime(InteropOSX.DISPATCH_TIME_NOW, 0);
        // 默认间隔为1秒
        InteropOSX.DispatchSourceSetTimer(timer, start, InteropOSX.NSEC_PER_SEC, 0);
    }
    public void SetPeriod(int periodMS)
    {
        if (running)
            // 暂停定时器
            InteropOSX.DispatchSuspend(timer);

        ulong start = InteropOSX.DispatchTime(InteropOSX.DISPATCH_TIME_NOW, 0);
        ulong interval = (ulong)periodMS * 1_000_000; // 转换为纳秒
        InteropOSX.DispatchSourceSetTimer(timer, start, interval, 0);
        if (running)
            // 恢复定时器
            InteropOSX.DispatchResume(timer);
    }

    private void Scheduler(IntPtr state)
    {
        _tick?.Invoke();
    }

    public void Start()
    {
        InteropOSX.DispatchResume(timer);
        running = true;
    }

    public void Stop()
    {
        InteropOSX.DispatchSuspend(timer);
        running = false;
    }

    public void Dispose()
    {
        InteropOSX.DispatchSourceCancel(timer);
        InteropOSX.DispatchRelease(timer);
    }
}