using System;
using System.Runtime.InteropServices;

namespace Eyu.Audio.Alsa;

public static class InteropAlsa
{
    const string AlsaLibrary = "libasound";

    #region PCM设备管理
    /// <summary>
    /// 获取错误字符串
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern IntPtr snd_strerror(int errnum);
    /// <summary>
    /// 打开音频设备
    /// </summary>
    [DllImport(AlsaLibrary)]
    internal static extern int snd_pcm_open(ref IntPtr pcm, string name, snd_pcm_stream_t stream, int mode);
    /// <summary>
    /// 启动音频播放
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern int snd_pcm_start(IntPtr pcm);
    /// <summary>
    /// 暂停音频播放
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern int snd_pcm_pause(IntPtr pcm, int enable);
    /// <summary>
    /// 恢复音频播放
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern int snd_pcm_resume(IntPtr pcm);
    /// <summary>
    /// 排空音频数据
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern int snd_pcm_drain(IntPtr pcm);
    /// <summary>
    /// 丢弃音频数据
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern int snd_pcm_drop(IntPtr pcm);
    /// <summary>
    /// 关闭音频设备
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern int snd_pcm_close(IntPtr pcm);

    /// <summary>
    /// 恢复音频设备错误
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern int snd_pcm_recover(IntPtr pcm, int err, int silent);
    /// <summary>
    /// 向音频设备写入数据
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern int snd_pcm_writei(IntPtr pcm, IntPtr buffer, ulong size);
    /// <summary>
    /// 从音频设备读取数据
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern int snd_pcm_readi(IntPtr pcm, IntPtr buffer, ulong size);
    /// <summary>
    /// 设置音频设备参数
    /// </summary>
    [DllImport(AlsaLibrary)]
    internal static extern int snd_pcm_set_params(IntPtr pcm, snd_pcm_format_t format, snd_pcm_access_t access, uint channels, uint rate, int soft_resample, uint latency);
    #endregion

    #region 硬件参数配置
    /// <summary>
    /// 分配音频设备参数内存
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern int snd_pcm_hw_params_malloc(ref IntPtr @params);

    /// <summary>
    /// 获取任意音频设备参数
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern int snd_pcm_hw_params_any(IntPtr pcm, IntPtr @params);
    /// <summary>
    /// 设置音频设备访问模式参数
    /// </summary>
    [DllImport(AlsaLibrary)]
    internal static extern int snd_pcm_hw_params_set_access(IntPtr pcm, IntPtr @params, snd_pcm_access_t access);

    /// <summary>
    /// 设置音频设备格式参数
    /// </summary>
    [DllImport(AlsaLibrary)]
    internal static extern int snd_pcm_hw_params_set_format(IntPtr pcm, IntPtr @params, snd_pcm_format_t val);

    /// <summary>
    /// 设置音频设备通道数参数
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern int snd_pcm_hw_params_set_channels(IntPtr pcm, IntPtr @params, uint val);

    /// <summary>
    /// 设置音频设备采样率参数
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern unsafe int snd_pcm_hw_params_set_rate_near(IntPtr pcm, IntPtr @params, uint* val, int* dir);

    /// <summary>
    /// 应用音频设备参数
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern int snd_pcm_hw_params(IntPtr pcm, IntPtr @params);

    /// <summary>
    /// 获取音频设备周期大小参数
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern unsafe int snd_pcm_hw_params_get_period_size(IntPtr @params, ulong* frames, int* dir);

    /// <summary>
    /// 设置音频设备周期大小参数
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern unsafe int snd_pcm_hw_params_set_period_size_near(IntPtr pcm, IntPtr @params, ulong* frames, int* dir);

    /// <summary>
    /// 设置音频设备缓冲区大小参数
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern int snd_pcm_hw_params_set_buffer_size_near(IntPtr elem, IntPtr @params, IntPtr size);
    #endregion

    #region 混音器管理
    /// <summary>
    /// 打开混音器
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern int snd_mixer_open(ref IntPtr mixer, int mode);

    /// <summary>
    /// 关闭混音器
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern int snd_mixer_close(IntPtr mixer);

    /// <summary>
    /// 将混音器关联到名称
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern int snd_mixer_attach(IntPtr mixer, string name);

    /// <summary>
    /// 加载混音器
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern int snd_mixer_load(IntPtr mixer);

    /// <summary>
    /// 注册混音器元素
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern int snd_mixer_selem_register(IntPtr mixer, IntPtr options, IntPtr classp);

    /// <summary>
    /// 获取第一个混音器元素
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern IntPtr snd_mixer_first_elem(IntPtr mixer);

    /// <summary>
    /// 获取下一个混音器元素
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern IntPtr snd_mixer_elem_next(IntPtr elem);

    /// <summary>
    /// 获取混音器元素名称
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern string snd_mixer_selem_get_name(IntPtr elem);

    /// <summary>
    /// 分配混音器元素内存
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern void snd_mixer_selem_id_alloca(IntPtr ptr);
    #endregion

    #region 播放控制
    /// <summary>
    /// 获取播放音量
    /// </summary>
    [DllImport(AlsaLibrary)]
    internal static extern unsafe int snd_mixer_selem_get_playback_volume(IntPtr elem, snd_mixer_selem_channel_id channel, long* value);

    /// <summary>
    /// 设置播放音量
    /// </summary>
    [DllImport(AlsaLibrary)]
    internal static extern int snd_mixer_selem_set_playback_volume(IntPtr elem, snd_mixer_selem_channel_id channel, long value);

    /// <summary>
    /// 设置所有播放音量
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern int snd_mixer_selem_set_playback_volume_all(IntPtr elem, long value);

    /// <summary>
    /// 设置所有播放开关
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern int snd_mixer_selem_set_playback_switch_all(IntPtr elem, int value);

    /// <summary>
    /// 获取播放音量范围
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern unsafe int snd_mixer_selem_get_playback_volume_range(IntPtr elem, long* min, long* max);

    /// <summary>
    /// 设置播放音量范围
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern int snd_mixer_selem_set_playback_volume_range(IntPtr elem, long min, long max);
    #endregion

    #region 录制控制
    /// <summary>
    /// 获取捕获音量
    /// </summary>
    [DllImport(AlsaLibrary)]
    internal static extern unsafe int snd_mixer_selem_get_capture_volume(IntPtr elem, snd_mixer_selem_channel_id channel, long* value);

    /// <summary>
    /// 设置捕获音量
    /// </summary>
    [DllImport(AlsaLibrary)]
    internal static extern int snd_mixer_selem_set_capture_volume(IntPtr elem, snd_mixer_selem_channel_id channel, long value);

    /// <summary>
    /// 设置所有捕获音量
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern int snd_mixer_selem_set_capture_volume_all(IntPtr elem, long value);

    /// <summary>
    /// 设置所有捕获开关
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern int snd_mixer_selem_set_capture_switch_all(IntPtr elem, int value);

    /// <summary>
    /// 获取捕获音量范围
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern unsafe int snd_mixer_selem_get_capture_volume_range(IntPtr elem, long* min, long* max);

    /// <summary>
    /// 设置捕获音量范围
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern int snd_mixer_selem_set_capture_volume_range(IntPtr elem, long min, long max);
    #endregion

    #region 音量/开关状态查询
    /// <summary>
    /// 检查混音器元素是否有播放音量控制
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern int snd_mixer_selem_has_playback_volume(IntPtr elem);
    
    /// <summary>
    /// 检查混音器元素是否有捕获音量控制
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern int snd_mixer_selem_has_capture_volume(IntPtr elem);
    
    /// <summary>
    /// 检查混音器元素是否有捕获开关控制
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern int snd_mixer_selem_has_capture_switch(IntPtr elem);
    
    /// <summary>
    /// 检查混音器元素是否有播放开关控制
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern int snd_mixer_selem_has_playback_switch(IntPtr elem);
    #endregion

    #region 设备枚举与控制
    /// <summary>
    /// 获取设备名称提示
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern int snd_device_name_hint(int card,string subSystem, IntPtr elem);
    
    /// <summary>
    /// 从设备名称提示中获取值
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern int snd_device_name_get_hint(IntPtr elem, string value);
    
    /// <summary>
    /// 获取下一张声卡
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern int snd_card_next(IntPtr elem);
    
    /// <summary>
    /// 打开控制接口
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern int snd_ctl_open(IntPtr elem, string name, int value);
    
    /// <summary>
    /// 关闭控制接口
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern int snd_ctl_close(IntPtr elem);
    
    /// <summary>
    /// 获取声卡信息
    /// </summary>
    [DllImport(AlsaLibrary)]
    public static extern int snd_ctl_card_info(IntPtr elem, string name, int value);
    #endregion
}
