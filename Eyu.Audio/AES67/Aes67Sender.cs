using Eyu.Audio.Provider;
using Eyu.Audio.PTP;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Linq;
using static Eyu.Audio.Aes67.Aes67Const;

namespace Eyu.Audio.Aes67;

/// <summary>
/// AES67音频流发送器，支持组播和单播模式
/// </summary>
public class Aes67Sender : IDisposable
{
    #region Properties

    private readonly PTPClient _ptpClient;
    private readonly PcmToRtpConverter _rtpConverter;
    private readonly UdpClient _udpClient;
    private readonly IPEndPoint _remoteEndpoint;
    private readonly WaveFormat _inputWaveFormat;
    private readonly WaveFormat _outputWaveFormat;
    private BufferedWaveProvider _inputProvider = null!;
    private IWaveProvider _outputProvider = null!;
    private readonly Channel<byte[]> _packetChannel;
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();
    private readonly System.Threading.Timer _sendTimer;
    private readonly int _samplesPerPacket;
    private readonly int _bytesPerPacket;
    private readonly bool _isMulticast;
    private byte[]? _currentPacket;
    private uint _sendFrameCount;

    public string Name { get; }
    public uint SessionId { get; }
    public bool IsSending { get; private set; }
    public IPEndPoint RemoteEndpoint => _remoteEndpoint;

    #endregion

    /// <summary>
    /// 构造AES67音频流发送器
    /// </summary>
    /// <param name="name">发送器名称</param>
    /// <param name="remoteEndpoint">远程端点（组播或单播）</param>
    /// <param name="inputWaveFormat">输入音频格式</param>
    /// <param name="sessionId">会话ID</param>
    /// <param name="ptpClient">PTP时间同步客户端</param>
    /// <param name="sampleRate">采样率，默认48000</param>
    /// <param name="bitsPerSample">采样位数，默认24</param>
    /// <param name="channels">声道数，默认2</param>
    /// <param name="payloadType">RTP负载类型，默认96</param>
    /// <param name="ptimeUs">包间隔时间(微秒)，默认250</param>
    public Aes67Sender(
        string name,
        IPEndPoint remoteEndpoint,
        WaveFormat inputWaveFormat,
        uint sessionId,
        PTPClient? ptpClient = null,
        int sampleRate = 48000,
        int bitsPerSample = 24,
        int channels = 2,
        byte payloadType = 96,
        uint ptimeUs = 250)
    {
        Name = name;
        SessionId = sessionId;
        _remoteEndpoint = remoteEndpoint;
        _inputWaveFormat = inputWaveFormat;
        _ptpClient = ptpClient ?? PTPClient.Instance;
        _isMulticast = IsMulticastAddress(remoteEndpoint.Address);

        // 设置输出格式为AES67标准格式
        _outputWaveFormat = new WaveFormat(sampleRate, bitsPerSample, channels);
        _samplesPerPacket = (int)Math.Ceiling(sampleRate * ptimeUs / 1000000f);
        _bytesPerPacket = _samplesPerPacket * channels * (bitsPerSample / 8);

        // 创建UDP客户端
        _udpClient = new UdpClient();
        if (_isMulticast)
        {
            _udpClient.Ttl = 16; // 组播TTL
            _udpClient.JoinMulticastGroup(remoteEndpoint.Address);
        }

        // 初始化RTP转换器
        _rtpConverter = new PcmToRtpConverter(
            _ptpClient,
            sampleRate,
            bitsPerSample,
            channels,
            payloadType,
            _samplesPerPacket,
            (uint)sessionId
        );

        // 构建音频处理链
        BuildAudioPipeline();

        // 创建数据包通道
        _packetChannel = Channel.CreateUnbounded<byte[]>();

        // 创建发送定时器，根据ptime计算间隔
        var intervalMs = (int)(ptimeUs / 1000);
        _sendTimer = new System.Threading.Timer(SendRtpPacket, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// 检查是否为组播地址
    /// </summary>
    private static bool IsMulticastAddress(IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] >= 224 && bytes[0] <= 239;
        }
        return false;
    }

    /// <summary>
    /// 构建音频处理管道
    /// </summary>
    private void BuildAudioPipeline()
    {
        _inputProvider = new BufferedWaveProvider(_inputWaveFormat);
        IWaveProvider waveProvider = _inputProvider;

        // 声道转换
        if (_inputWaveFormat.Channels == 1 && _outputWaveFormat.Channels == 2)
        {
            waveProvider = new MonoToStereoProvider16(waveProvider);
        }
        else if (_inputWaveFormat.Channels == 2 && _outputWaveFormat.Channels == 1)
        {
            waveProvider = new StereoToMonoProvider16(waveProvider);
        }

        // 采样率转换
        if (_inputWaveFormat.SampleRate != _outputWaveFormat.SampleRate)
        {
            var sampleProvider = new SampleChannel(waveProvider, false);
            var resampledProvider = new SampleWaveFormatConversionProvider(
                new WaveFormat(_outputWaveFormat.SampleRate, waveProvider.WaveFormat.Channels),
                sampleProvider);
            waveProvider = new SampleToWaveProvider(resampledProvider);
        }

        // 位深转换
        if (_inputWaveFormat.BitsPerSample != _outputWaveFormat.BitsPerSample)
        {
            var sampleProvider = new SampleChannel(waveProvider, false);
            switch (_outputWaveFormat.BitsPerSample)
            {
                case 16:
                    waveProvider = new SampleToWaveProvider16(sampleProvider);
                    break;
                case 24:
                    waveProvider = new SampleToWaveProvider24(sampleProvider);
                    break;
                case 32:
                    waveProvider = new SampleToWaveProvider(sampleProvider);
                    break;
                default:
                    throw new ArgumentException($"Unsupported bits per sample: {_outputWaveFormat.BitsPerSample}");
            }
        }

        _outputProvider = waveProvider;
    }

    /// <summary>
    /// 开始发送音频流
    /// </summary>
    public void Start()
    {
        if (IsSending) return;

        IsSending = true;
        _rtpConverter.Initialize();

        // 启动发送定时器
        var intervalMs = (int)(DefaultPTimeμs / 1000);
        _sendTimer.Change(0, intervalMs);

        // 如果是组播，发送SDP通告
        if (_isMulticast)
        {
            SendSdpAnnouncement();
        }
    }

    /// <summary>
    /// 停止发送音频流
    /// </summary>
    public void Stop()
    {
        if (!IsSending) return;

        IsSending = false;
        _sendTimer.Change(Timeout.Infinite, Timeout.Infinite);

        // 如果是组播，发送SDP删除通告
        if (_isMulticast)
        {
            SendSdpDeletion();
        }
    }

    /// <summary>
    /// 写入音频数据
    /// </summary>
    /// <param name="buffer">音频数据缓冲区</param>
    /// <param name="offset">偏移量</param>
    /// <param name="count">字节数</param>
    public void WriteAudio(byte[] buffer, int offset, int count)
    {
        if (!IsSending) return;

        try
        {
            // 确保参数有效
            if (buffer == null || offset < 0 || count <= 0 || offset + count > buffer.Length)
                return;

            // 添加到输入缓冲区
            _inputProvider.AddSamples(buffer, offset, count);

            // 处理音频数据并生成RTP包
            ProcessAudioData();
        }
        catch (Exception ex)
        {
            // 记录错误但不中断发送
            Console.WriteLine($"Error writing audio data: {ex.Message}");
        }
    }

    /// <summary>
    /// 处理音频数据并生成RTP包
    /// </summary>
    private void ProcessAudioData()
    {
        // 计算输出缓冲区大小
        var ratio = (double)(_outputWaveFormat.SampleRate * _outputWaveFormat.BitsPerSample * _outputWaveFormat.Channels) /
                   (_inputWaveFormat.SampleRate * _inputWaveFormat.BitsPerSample * _inputWaveFormat.Channels);
        var outputBufferSize = (int)(_inputProvider.BufferedBytes * ratio);

        if (outputBufferSize < _bytesPerPacket)
            return; // 不足一个包

        // 确保输出缓冲区大小是包大小的整数倍
        outputBufferSize = outputBufferSize - (outputBufferSize % _bytesPerPacket);
        var outputBuffer = new byte[outputBufferSize];
        var bytesRead = _outputProvider.Read(outputBuffer, 0, outputBufferSize);

        // 分割成RTP包
        int offset = 0;
        while (offset < bytesRead)
        {
            var packetSize = Math.Min(_bytesPerPacket, bytesRead - offset);
            var packet = new byte[packetSize];
            Array.Copy(outputBuffer, offset, packet, 0, packetSize);
            _packetChannel.Writer.TryWrite(packet);
            offset += packetSize;
        }
    }

    /// <summary>
    /// 定时器回调：发送RTP包
    /// </summary>
    private void SendRtpPacket(object? state)
    {
        if (!IsSending) return;

        try
        {
            // 定期发送SDP通告（组播模式）
            if (_isMulticast)
            {
                _sendFrameCount++;
                if (_sendFrameCount * DefaultPTimeμs >= 10_000_000) // 每10秒
                {
                    _sendFrameCount = 0;
                    SendSdpAnnouncement();
                }
            }

            // 获取当前数据包
            if (_currentPacket == null)
            {
                if (_packetChannel.Reader.TryRead(out var packet))
                {
                    _currentPacket = packet;
                }
            }

            // 发送RTP包
            if (_currentPacket != null && _rtpConverter.EnsureTime())
            {
                var rtpPacket = _rtpConverter.BuildRtpPacket(_currentPacket, 0, _currentPacket.Length);
                if (rtpPacket != null)
                {
                    _udpClient.SendAsync(rtpPacket, rtpPacket.Length, _remoteEndpoint);
                    _currentPacket = null;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending RTP packet: {ex.Message}");
        }
    }

    /// <summary>
    /// 发送SDP通告
    /// </summary>
    private void SendSdpAnnouncement()
    {
        try
        {
            var sdp = new Sdp(
                Name,
                SessionId,
                GetLocalIPAddress().ToString(),
                _remoteEndpoint.Address.ToString(),
                _remoteEndpoint.Port,
                _ptpClient.PtpMaster,
                _ptpClient.Domain,
                DefaultPTimeμs / 1000f,
                _samplesPerPacket,
                DefaultEncoding,
                _outputWaveFormat.SampleRate,
                _outputWaveFormat.Channels,
                0,
                $"AES67 Audio Stream: {Name}"
            );

            var sapPacket = sdp.BuildSap(false);
            _udpClient.SendAsync(sapPacket, sapPacket.Length, Aes67Const.SdpMulticastIPEndPoint);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending SDP announcement: {ex.Message}");
        }
    }

    /// <summary>
    /// 发送SDP删除通告
    /// </summary>
    private void SendSdpDeletion()
    {
        try
        {
            var sdp = new Sdp(
                Name,
                SessionId,
                GetLocalIPAddress().ToString(),
                _remoteEndpoint.Address.ToString(),
                _remoteEndpoint.Port,
                _ptpClient.PtpMaster,
                _ptpClient.Domain,
                DefaultPTimeμs / 1000f,
                _samplesPerPacket,
                DefaultEncoding,
                _outputWaveFormat.SampleRate,
                _outputWaveFormat.Channels,
                0,
                $"AES67 Audio Stream: {Name}"
            );

            var sapPacket = sdp.BuildSap(true);
            _udpClient.SendAsync(sapPacket, sapPacket.Length, Aes67Const.SdpMulticastIPEndPoint);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending SDP deletion: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取本地IP地址
    /// </summary>
    private IPAddress GetLocalIPAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                {
                    return ip;
                }
            }
        }
        catch
        {
            // 忽略异常，返回默认值
        }
        return IPAddress.Parse("127.0.0.1");
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Stop();
        _sendTimer?.Dispose();
        _udpClient?.Dispose();
        _cts?.Dispose();
        _packetChannel?.Writer.Complete();
    }
}