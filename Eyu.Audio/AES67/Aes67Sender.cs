using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eyu.Audio.AES67;

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

/// <summary>
/// AES67广播发送器，遵循AES67标准实现专业音频流的RTP广播
/// </summary>
public class Aes67Broadcaster : IDisposable
{
    private readonly Sdp _sdp;
    private readonly UdpClient _udpClient;
    private readonly PcmToRtpConverter _rtpConverter;
    private readonly IPEndPoint _multicastEndpoint;
    private bool IsRunning;
    private readonly float _frameIntervalMs;
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();
    public const string rptMuticastAddress = "239.69.";
    /// <summary>
    /// 构造AES67广播发送器
    /// </summary>
    /// <param name="sdp">SDP会话描述</param>
    public Aes67Broadcaster(IWaveProvider waveProvider,
                            PTPClient pTPClient,
                            float ptime,
                            string name,
                            uint sessId,
                            string localAddress,
                            string muticastAddress,
                            int muticastPort,
                            string encoding = "L24",
                            string? info = null)
    {
        _sdp = new Sdp(name, sessId, localAddress, muticastAddress, muticastPort, pTPClient.PtpMaster, pTPClient.Domain, ptime, encoding, waveProvider.WaveFormat.SampleRate, waveProvider.WaveFormat.Channels, info);
        if (waveProvider == null) throw new ArgumentNullException(nameof(waveProvider));
        //_sdp = sdp ?? throw new ArgumentNullException(nameof(sdp));
        // 验证AES67所需的音频参数
        ValidateAes67Parameters();

        // 初始化多播端点
        _multicastEndpoint = new IPEndPoint(IPAddress.Parse(_sdp.MuticastAddress), _sdp.MuticastPort);

        // 初始化UDP客户端并配置多播选项
        _udpClient = new UdpClient();
        ConfigureUdpClient();

        // 初始化RTP转换器
        _rtpConverter = new PcmToRtpConverter(
            payloadType: 96,
            waveProvider: waveProvider,
            pTPClient: pTPClient,
            ssrc: _sdp.SessId,
            packageTime: _sdp.Ptime
        );

        // 计算帧发送间隔(ms)
        _frameIntervalMs = _sdp.Ptime;

    }

    /// <summary>
    /// 启动AES67广播
    /// </summary>
    public void Start()
    {
        if (IsRunning) return;

        IsRunning = true;
    }

    /// <summary>
    /// 停止AES67广播
    /// </summary>
    public void Stop()
    {
        if (!IsRunning) return;
        IsRunning = false;
    }

    /// <summary>
    /// 写入PCM音频数据到发送缓冲区
    /// </summary>
    /// <param name="buffer">PCM音频缓冲区</param>
    /// <param name="offset">偏移量</param>
    /// <param name="count">数据长度</param>
    //public void WriteAudio(byte[] buffer, int offset, int count)
    //{
    //    if (!IsRunning)
    //        throw new InvalidOperationException("广播未启动，请先调用Start()");

    //    if (buffer == null) throw new ArgumentNullException(nameof(buffer));
    //    if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
    //    if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
    //    if (offset + count > buffer.Length) throw new ArgumentException("缓冲区溢出");

    //    _rtpConverter.Write(buffer, offset, count);
    //}

    /// <summary>
    /// 发送RTP帧的定时器回调
    /// </summary>
    private void SendRtpFrames()
    {
        if (!IsRunning) return;

        try
        {
            // 从转换器获取RTP包并发送
            byte[] rtpFrame = _rtpConverter.ReadRtpFrame();
            if (rtpFrame != null && rtpFrame.Length > 0)
            {
                _udpClient.Send(rtpFrame, rtpFrame.Length, _multicastEndpoint);
            }
        }
        catch (Exception ex)
        {
            // 处理发送错误
            Console.WriteLine($"发送RTP帧时出错: {ex.Message}");
        }
    }

   
    /// <summary>
    /// 配置UDP客户端的多播选项
    /// </summary>
    private void ConfigureUdpClient()
    {
        // 设置多播TTL
        _udpClient.Ttl = 16;

        // 加入多播组
        var multicastAddress = IPAddress.Parse(_sdp.MuticastAddress);
        _udpClient.JoinMulticastGroup(multicastAddress);

        // 如果指定了本地地址，绑定到该地址
        if (!string.IsNullOrEmpty(_sdp.LocalAddress) && IPAddress.TryParse(_sdp.LocalAddress, out var localAddr))
        {
            _udpClient.Client.Bind(new IPEndPoint(localAddr, 0));
        }
    }

    /// <summary>
    /// 验证AES67所需的参数
    /// </summary>
    private void ValidateAes67Parameters()
    {
        // AES67要求支持的采样率: 44.1kHz, 48kHz, 88.2kHz, 96kHz, 176.4kHz, 192kHz
        var supportedSampleRates = new[] { 44100, 48000, 88200, 96000, 176400, 192000 };
        if (!Array.Exists(supportedSampleRates, rate => rate == _sdp.SampleRate))
        {
            throw new ArgumentException($"AES67不支持的采样率: {_sdp.SampleRate}. 支持的采样率: {string.Join(", ", supportedSampleRates)}");
        }

        // AES67要求支持的编码: L16, L24
        if (_sdp.AudioEncoding != "L16" && _sdp.AudioEncoding != "L24")
        {
            throw new ArgumentException($"AES67不支持的编码格式: {_sdp.AudioEncoding}. 仅支持L16和L24");
        }

        // 验证ptime (AES67通常使用0.125ms到100ms)
        if (_sdp.Ptime <= 0 || _sdp.Ptime > 100)
        {
            throw new ArgumentException($"ptime值超出AES67推荐范围 (0.125-100ms): {_sdp.Ptime}");
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Stop();
        _cts.Dispose();
        _udpClient?.DropMulticastGroup(IPAddress.Parse(_sdp.MuticastAddress));
        _udpClient?.Dispose();
    }
}