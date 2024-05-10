using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace Eyu.Audio.Timer;


/// <summary>
/// 高精度定时器
/// </summary>
[SupportedOSPlatform("windows")]
public class HPTimer
{
    static HPTimer()
    {
        TimeGetDevCaps(ref _caps, Marshal.SizeOf(_caps));
    }

    public static HPTimer Run(int interval, Action ticked)
    {
        var timer = new HPTimer(interval, ticked);
        timer.Start();
        return timer;
    }


    public HPTimer()
    {
        Running = false;
        _interval = _caps.periodMin;
        _resolution = _caps.periodMin;
        _callback = new TimerCallback(TimerEventCallback);
    }
    public HPTimer(int interval, Action ticked) : this()
    {
        _interval = interval;
        Ticked = ticked;
    }

    ~HPTimer()
    {
        TimeKillEvent(_id);
    }

    /// <summary>
    /// 系统定时器回调
    /// </summary>
    /// <param name="id">定时器编号</param>
    /// <param name="msg">预留，不使用</param>
    /// <param name="user">用户实例数据</param>
    /// <param name="param1">预留，不使用</param>
    /// <param name="param2">预留，不使用</param>
    private delegate void TimerCallback(int id, int msg, int user, int param1, int param2);

    #region 动态库接口
    /// <summary>
    /// 用于提高计时器精度。
    /// </summary>
    /// <param name="uMilliseconds"></param>
    /// <returns></returns>
    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    public static extern uint TimeBeginPeriod(uint uMilliseconds);
    /// <summary>
    /// 恢复计时器精度。
    /// </summary>
    /// <param name="uMilliseconds"></param>
    /// <returns></returns>
    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    public static extern uint TimeEndPeriod(uint uMilliseconds);
    /// <summary>
    /// 查询设备支持的定时器分辨率
    /// </summary>
    /// <param name="ptc">定时器分辨率结构体指针</param>
    /// <param name="cbtc">定时器分辨率结构体大小</param>
    /// <returns></returns>
    [DllImport("winmm.dll", EntryPoint = "timeGetDevCaps")]
    private static extern TimerError TimeGetDevCaps(ref TimerCaps ptc, int cbtc);

    /// <summary>
    /// 绑定定时器事件
    /// </summary>
    /// <param name="delay">延时：毫秒</param>
    /// <param name="resolution">分辨率</param>
    /// <param name="callback">回调接口</param>
    /// <param name="user">用户提供的回调数据</param>
    /// <param name="eventType"></param>
    [DllImport("winmm.dll", EntryPoint = "timeSetEvent")]
    private static extern int TimeSetEvent(int delay, int resolution, TimerCallback callback, int user, int eventType);

    /// <summary>
    /// 终止定时器
    /// </summary>
    /// <param name="id">定时器编号</param>
    [DllImport("winmm.dll", EntryPoint = "timeKillEvent")]
    private static extern TimerError TimeKillEvent(int id);

    #endregion

    #region 属性

    /// <summary>时间间隔：毫秒</summary>
    public int Interval
    {
        get
        {
            return _interval;
        }
        set
        {
            if (value < _caps.periodMin)
                _interval = _caps.periodMin;
            else if (value > _caps.periodMax)
                _interval = _caps.periodMax;
            else _interval = value;
        }
    }

    public bool Running
    {
        get; private set;
    }

    #endregion

    #region 事件

    public event Action Ticked;

    #endregion

    #region 公开方法

    public void Start()
    {
        if (!Running)
        {
            _id = TimeSetEvent(_interval, _resolution, _callback, 0,
                (int)EventType01.TIME_PERIODIC | (int)EventType02.TIME_KILL_SYNCHRONOUS);
            if (_id == 0) throw new Exception("启动定时器失败");
            Running = true;
        }
    }

    public void Stop()
    {
        if (Running)
        {
            TimeKillEvent(_id);
            Running = false;
        }
    }

    #endregion

    #region 内部方法

    private void TimerEventCallback(int id, int msg, int user, int param1, int param2)
    {
        Ticked?.Invoke();
    }

    #endregion

    #region 字段

    // 系统定时器分辨率
    private static TimerCaps _caps;
    // 定时器间隔
    private int _interval;
    // 定时器分辨率
    private int _resolution;
    // 定时器回调
    private TimerCallback _callback;
    // 定时器编号
    private int _id;

    #endregion
}


public enum TimerError
{
    /// <summary>没有错误</summary>
    MMSYSERR_NOERROR = 0,

    /// <summary>常规错误码</summary>
    MMSYSERR_ERROR = 1,
    /// <summary>ptc 为空，或 cbtc 无效，或其他错误</summary>
    TIMERR_NOCANDO = 97,
    /// <summary>无效参数</summary>
    MMSYSERR_INVALPARAM = 11,
}

public enum EventType01
{
    /// <summary>单次执行</summary>
    TIME_ONESHOT = 0x0000,
    /// <summary>循环执行</summary>
    TIME_PERIODIC = 0x0001,
}

public enum EventType02
{
    /// <summary>定时器到期时，调用回调方法。这是默认值</summary>
    TIME_CALLBACK_FUNCTION = 0x0000,
    /// <summary>定时器到期时，调用 setEvent</summary>
    TIME_CALLBACK_EVENT_SET = 0x0010,
    /// <summary>定时器到期时，调用 PulseEvent</summary>
    TIME_CALLBACK_EVENT_PULSE = 0x0020,
    /// <summary>防止在调用 timeKillEvent 函数之后发生事件</summary>
    TIME_KILL_SYNCHRONOUS = 0x0100,
}

/// <summary>
/// 定时器分辨率：毫秒
/// </summary>
struct TimerCaps
{
    /// <summary>最小周期</summary>
    public int periodMin;
    /// <summary>最大周期</summary>
    public int periodMax;
}
