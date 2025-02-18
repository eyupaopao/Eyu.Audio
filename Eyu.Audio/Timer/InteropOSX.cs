using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Eyu.Audio.Timer;
internal enum DispatchTime
{
    Now = 0,
    Forever = ~0
}
internal class InteropOSX
{ // 导入 libdispatch 库
    public const string LibDispatch = "/usr/lib/system/libdispatch.dylib";

    // 定义 dispatch_queue_t 类型
    public static readonly IntPtr DispatchQueueMain = (IntPtr)1; // 主队列

    // 定义 dispatch_source_t 类型
    public struct DispatchSource { }

    public const ulong DISPATCH_SOURCE_TYPE_TIMER = 0x1f63e0980;
    public const ulong NSEC_PER_SEC = 1000000000;
    public const ulong DISPATCH_TIME_NOW = 0;

    // 定义 dispatch_time_t 类型
    [DllImport(LibDispatch, EntryPoint = "dispatch_queue_create", CharSet = CharSet.Ansi)]
    public static extern IntPtr DispatchQueueCreate(string label, IntPtr attr);

    // 定义 dispatch_source_create 函数
    [DllImport(LibDispatch, EntryPoint = "dispatch_source_create")]
    public static extern IntPtr DispatchSourceCreate(ulong type, ulong handle, ulong mask, IntPtr queue);

    // 定义 dispatch_source_set_timer 函数
    [DllImport(LibDispatch, EntryPoint = "dispatch_source_set_timer")]
    public static extern void DispatchSourceSetTimer(IntPtr source, ulong start, ulong interval, ulong leeway);

    // 定义 dispatch_source_set_event_handler 函数
    [DllImport(LibDispatch, EntryPoint = "dispatch_source_set_event_handler_f")]
    public static extern void DispatchSourceSetEventHandler(IntPtr source, DispatchBlock handler);

    // 定义 dispatch_resume 函数
    [DllImport(LibDispatch, EntryPoint = "dispatch_resume")]
    public static extern void DispatchResume(IntPtr source);

    [DllImport(LibDispatch, EntryPoint = "dispatch_suspend")]
    public static extern void DispatchSuspend(IntPtr source);

    // 定义 dispatch_source_cancel 函数
    [DllImport(LibDispatch, EntryPoint = "dispatch_source_cancel")]
    public static extern void DispatchSourceCancel(IntPtr source);

    // 定义 dispatch_release 函数
    [DllImport(LibDispatch, EntryPoint = "dispatch_release")]
    public static extern void DispatchRelease(IntPtr source);

    // 定义 dispatch_time 函数
    [DllImport(LibDispatch, EntryPoint = "dispatch_time")]
    public static extern ulong DispatchTime(ulong when, long delta);

    // 定义 dispatch_block_t 类型
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void DispatchBlock(IntPtr ptr);

    // static void Main(string[] args)
    // {
    //     // 创建一个定时器
    //     IntPtr timer = DispatchSourceCreate(1, 0, 0, DispatchQueueMain); // 1 表示 DISPATCH_SOURCE_TYPE_TIMER

    //     // 设置定时器的开始时间、间隔和误差
    //     ulong start = DispatchTime(0, (long)(2 * 1e9)); // 2 秒后开始
    //     ulong interval = (ulong)(1 * 1e9); // 每 1 秒触发一次
    //     ulong leeway = (ulong)(0.1 * 1e9); // 允许 0.1 秒的误差
    //     DispatchSourceSetTimer(timer, start, interval, leeway);

    //     // 设置定时器的事件处理程序
    //     DispatchBlock block = () =>
    //     {
    //         Console.WriteLine("Timer fired at: " + DateTime.Now);
    //     };
    //     IntPtr blockPtr = Marshal.GetFunctionPointerForDelegate(block);
    //     DispatchSourceSetEventHandler(timer, blockPtr);

    //     // 启动定时器
    //     DispatchResume(timer);

    //     // 主线程等待 10 秒
    //     Thread.Sleep(10000);

    //     // 取消定时器
    //     DispatchSourceCancel(timer);
    //     DispatchRelease(timer);

    //     Console.WriteLine("Timer stopped.");
    // }
}


public class OSXTimer : ITimer
{
    static OSXTimer()
    {
        queue = InteropOSX.DispatchQueueCreate("osx timer", 0);
    }

    private IntPtr timer;
    private static IntPtr queue;
    private Action _tick;
    public OSXTimer(Action tick)
    {
        _tick = tick;

        if (queue == IntPtr.Zero)
        {
            throw new Exception($"Unable to create timer, errno = {Marshal.GetLastPInvokeError()}");
        }
        timer = InteropOSX.DispatchSourceCreate(InteropOSX.DISPATCH_SOURCE_TYPE_TIMER, 0, 0, queue);
        if (timer == IntPtr.Zero)
            throw new Exception($"Unable to create timer, errno = {Marshal.GetLastPInvokeError()}");

        InteropOSX.DispatchSourceSetEventHandler(timer, Scheduler);
        var start = InteropOSX.DispatchTime(InteropOSX.DISPATCH_TIME_NOW, 0);

        InteropOSX.DispatchSourceSetTimer(timer, start, InteropOSX.NSEC_PER_SEC / 1, 0);

    }
    public void SetPeriod(int periodMS)
    {

    }

    private void Scheduler(IntPtr state)
    {
        _tick();
    }

    public void Start()
    {
        InteropOSX.DispatchResume(timer);
    }

    public void Stop()
    {
        InteropOSX.DispatchSuspend(timer);
    }

    public void Dispose()
    {
        InteropOSX.DispatchSourceCancel(timer);
        InteropOSX.DispatchRelease(timer);
    }
}