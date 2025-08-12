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
{
    // 导入 libdispatch 库
    const string LibDispatch = "/usr/lib/system/libdispatch.dylib";

    // 定义 dispatch_queue_t 类型
    public static readonly IntPtr DispatchQueueMain = (IntPtr)1; // 主队列

    // 定义 dispatch_source_t 类型
    public struct DispatchSource { }

    public const ulong DISPATCH_SOURCE_TYPE_TIMER = 0x1f63e0980;
    public const ulong NSEC_PER_SEC = 1000000000;
    public const ulong DISPATCH_TIME_NOW = 0;

    /// <summary>
    /// 创建一个新的调度队列
    /// </summary>
    /// <param Name="label">队列的标签</param>
    /// <param Name="attr">队列的属性</param>
    /// <returns>返回新创建的调度队列的指针</returns>
    [DllImport(LibDispatch, EntryPoint = "dispatch_queue_create", CharSet = CharSet.Ansi)]
    public static extern IntPtr DispatchQueueCreate(string label, IntPtr attr);

    /// <summary>
    /// 创建一个新的调度源
    /// </summary>
    /// <param Name="type">调度源的类型</param>
    /// <param Name="handle">调度源的句柄</param>
    /// <param Name="mask">调度源的掩码</param>
    /// <param Name="queue">调度源所属的队列</param>
    /// <returns>返回新创建的调度源的指针</returns>
    [DllImport(LibDispatch, EntryPoint = "dispatch_source_create")]
    public static extern IntPtr DispatchSourceCreate(ulong type, ulong handle, ulong mask, IntPtr queue);

    /// <summary>
    /// 设置调度源的定时器
    /// </summary>
    /// <param Name="source">调度源</param>
    /// <param Name="start">定时器的开始时间</param>
    /// <param Name="interval">定时器的间隔时间</param>
    /// <param Name="leeway">定时器的宽限时间</param>
    [DllImport(LibDispatch, EntryPoint = "dispatch_source_set_timer")]
    public static extern void DispatchSourceSetTimer(IntPtr source, ulong start, ulong interval, ulong leeway);

    /// <summary>
    /// 设置调度源的事件处理程序
    /// </summary>
    /// <param Name="source">调度源</param>
    /// <param Name="handler">事件处理程序</param>
    [DllImport(LibDispatch, EntryPoint = "dispatch_source_set_event_handler_f")]
    public static extern void DispatchSourceSetEventHandler(IntPtr source, DispatchBlock handler);

    /// <summary>
    /// 恢复调度源
    /// </summary>
    /// <param Name="source">调度源</param>
    [DllImport(LibDispatch, EntryPoint = "dispatch_resume")]
    public static extern void DispatchResume(IntPtr source);

    /// <summary>
    /// 暂停调度源
    /// </summary>
    /// <param Name="source">调度源</param>
    [DllImport(LibDispatch, EntryPoint = "dispatch_suspend")]
    public static extern void DispatchSuspend(IntPtr source);

    /// <summary>
    /// 取消调度源
    /// </summary>
    /// <param Name="source">调度源</param>
    [DllImport(LibDispatch, EntryPoint = "dispatch_source_cancel")]
    public static extern void DispatchSourceCancel(IntPtr source);

    /// <summary>
    /// 释放调度源
    /// </summary>
    /// <param Name="source">调度源</param>
    [DllImport(LibDispatch, EntryPoint = "dispatch_release")]
    public static extern void DispatchRelease(IntPtr source);

    /// <summary>
    /// 获取调度时间
    /// </summary>
    /// <param Name="when">起始时间</param>
    /// <param Name="delta">时间增量</param>
    /// <returns>返回计算后的调度时间</returns>
    [DllImport(LibDispatch, EntryPoint = "dispatch_time")]
    public static extern ulong DispatchTime(ulong when, long delta);

    /// <summary>
    /// 定义调度块类型
    /// </summary>
    /// <param Name="ptr">指向调度块的指针</param>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void DispatchBlock(IntPtr ptr);
}