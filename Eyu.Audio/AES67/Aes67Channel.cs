using Eyu.Audio.Provider;
using Eyu.Audio.PTP;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;

namespace Eyu.Audio.Aes67;

/// <summary>
/// AES67广播发送器，遵循AES67标准实现专业音频流的RTP广播
/// </summary>
public class Aes67Channel : IDisposable
{
    #region properties

    public readonly int SamplesPerPacket;
    public readonly Dictionary<IPAddress, Sdp> Sdps = new();
    private readonly Dictionary<IPAddress, UdpClient> _udpClients = new();
    private readonly PcmToRtpConverter _rtpConverter;
    public readonly IPEndPoint MulticastEndpoint;
    ConcurrentQueue<byte[]> _packets = new();
    private int bytesPerPacket;
    byte[]? currentPacket = null!;
    public int GetPackageCount() {
        return _packets.Count;
    }

    /// <summary>
    /// 清除缓存。当用户控制调整进度（如 seek）时调用，清空发送队列并重置媒体时间戳。
    /// </summary>
    public void ClearCache()
    {
        while (_packets.TryDequeue(out _)) { }
        currentPacket = null;
        _rtpConverter.ResetTimestamp();
        _lastSendTime = DateTime.UtcNow;
    }
    private WaveFormat _inputWaveFormat = null!;
    public WaveFormat OutputWaveFormat { get; set; }
    public uint PTimμs { get; }

    // 包间隔(μs)推荐包间隔：48k 125μs,250μs,333μs; 96k:250μs,1000μs,4000μs
    private uint _sendFrameCount;
    // 上次成功发送包的时间，用于空闲过久时对时
    private DateTime _lastSendTime = DateTime.UtcNow;
    private const int IdleSyncThresholdMs = 500;
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();
    private readonly List<IPAddress> localAddresses;
    public readonly string name;
    BufferedWaveProvider _inputProvider = null!;
    IWaveProvider _outputProvider = null!;
    public uint SessId { get; }
    internal IPAddress MuticastAddress { get; }
    #endregion

    #region constructor

    /// <summary>
    /// 构造AES67广播发送器
    /// </summary>
    /// <param mediaName="sdp">SDP会话描述</param>
    public Aes67Channel(uint sessId, List<IPAddress> localAddresses, IPAddress muticastAddress, int muticastPort, string name, uint pTimeμs = Aes67Const.DefaultPTimeμs, string? title = null)
    {
        PTimμs = pTimeμs;
        // 强制统一输出格式。
        OutputWaveFormat = new WaveFormat(Aes67Const.DefaultSampleRate, Aes67Const.DefaultBitsPerSample, Aes67Const.DefaultChannels);
        SamplesPerPacket = (int)Math.Ceiling(OutputWaveFormat.SampleRate * PTimμs / 1000000f);
        var pTPClient = PTPClient.Instance;
        foreach (var address in localAddresses)
        {
            var sdp = new Sdp(name,
                       sessId,
                       address.ToString(),
                       muticastAddress.ToString(),
                       muticastPort,
                       pTPClient.PtpMaster,
                       pTPClient.Domain,
                       PTimμs / 1000f,
                       SamplesPerPacket,
                       Aes67Const.DefaultEncoding,
                       OutputWaveFormat.SampleRate,
                       OutputWaveFormat.Channels,
                       0,
                       title);
            ValidateAes67Parameters(sdp);
            var udpClient = new UdpClient(new IPEndPoint(address, 0));
            udpClient.Ttl = 16;
            _udpClients[address] = udpClient;
            Sdps[address] = sdp;
        }
        SessId = sessId;
        this.localAddresses = localAddresses;
        MuticastAddress = muticastAddress;
        this.name = name;
        // 初始化多播端点
        MulticastEndpoint = new IPEndPoint(MuticastAddress, muticastPort);
        // 初始化RTP转换器
        _rtpConverter = new PcmToRtpConverter(
            pTPClient, OutputWaveFormat.SampleRate, OutputWaveFormat.BitsPerSample, OutputWaveFormat.Channels,
            (byte)Aes67Const.DefaultPayloadType,
            SamplesPerPacket,SessId
        );
    }
    /// <summary>
    /// 验证AES67所需的参数
    /// </summary>
    private void ValidateAes67Parameters(Sdp sdp)
    {
        if (!Array.Exists(Aes67Const.SupportedSampleRates, rate => rate == sdp.SampleRate))
        {
            throw new ArgumentException($"AES67 is not support sample rate: {sdp.SampleRate}. please use: {string.Join(", ", Aes67Const.SupportedSampleRates)}");
        }

        // AES67要求支持的编码: L16, L24
        if (sdp.AudioEncoding != "L16" && sdp.AudioEncoding != "L24")
        {
            throw new ArgumentException($"AES67 is not support encoding: {sdp.AudioEncoding}. please use L16 or L24");
        }

        // 验证ptime (AES67通常使用0.125ms到100ms)
        if (sdp.PTimems <= 0 || sdp.PTimems > 100)
        {
            throw new ArgumentException($"ptime out of range (0.125-100ms): {sdp.PTimems}");
        }
    }
   
    #endregion

    #region init

    internal void Init(WaveFormat inputWaveFormat, string title)
    {
        if (_inputWaveFormat == null || !_inputWaveFormat.Equals(inputWaveFormat))
        {
            _inputWaveFormat = inputWaveFormat;
            BuildProvider();
        }
        if (!string.IsNullOrEmpty(title))
        {
            foreach (var sdp in Sdps.Values)
            {
                sdp.SetTitle(title);
            }
        }
        _rtpConverter.Initialize();
        _lastSendTime = DateTime.UtcNow;
        SendSdp();
    }
    void BuildProvider()
    {
        _inputProvider = new BufferedWaveProvider(_inputWaveFormat);
        IWaveProvider waveProvider = _inputProvider;
        if (_inputWaveFormat.Channels == 1 && OutputWaveFormat.Channels == 2)
        {
            waveProvider = new MonoToStereoProvider16(waveProvider);
        }
        if (_inputWaveFormat.SampleRate == OutputWaveFormat.SampleRate && _inputWaveFormat.BitsPerSample == OutputWaveFormat.BitsPerSample)
        {
            _outputProvider = waveProvider;
            return;
        }
        ISampleProvider sampleProvider = new SampleChannel(waveProvider, false);
        // 采样率不一样的需要重采样。
        if (waveProvider.WaveFormat.SampleRate != OutputWaveFormat.SampleRate)
            sampleProvider = new SampleWaveFormatConversionProvider(new WaveFormat(OutputWaveFormat.SampleRate, sampleProvider.WaveFormat.Channels), sampleProvider);
        // 位深不一样的，需要转换位深。
        switch (OutputWaveFormat.BitsPerSample)
        {
            case 16:
                _outputProvider = new SampleToWaveProvider16(sampleProvider);
                break;
            case 24:
                _outputProvider = new SampleToWaveProvider24(sampleProvider);
                break;
            case 32:
                _outputProvider = new SampleToWaveProvider(sampleProvider);
                break;
            default:
                throw new ArgumentException($"not support encoding：{OutputWaveFormat.BitsPerSample}");
        }
        // 大小端转换：AES67/RFC 要求 L24 为网络字节序（大端），NAudio 输出为小端，需按样本翻转
        _outputProvider = new PCMSwappingProvider(_outputProvider);
        bytesPerPacket = SamplesPerPacket * _outputProvider.WaveFormat.Channels * (_outputProvider.WaveFormat.BitsPerSample / 8);
    }

    #endregion

    #region sdp

    public void SetMediaName(string? mediaName)
    {
        if (string.IsNullOrEmpty(mediaName)) return;
        foreach (var sdp in Sdps.Values)
        {
            sdp.SetTitle(mediaName);
        }
    }
    private void SendSdp(bool deletion = false)
    {
        foreach (var address in _udpClients.Keys)
        {
            if (Sdps.TryGetValue(address, out var sdp))
            {
                _udpClients[address].SendAsync(sdp.BuildSap(deletion), Aes67Const.SdpMulticastIPEndPoint);
            }
        }
    }
    #endregion
    public void Write(byte[] bytes, int offset, int count)
    {
        try
        {
            if (bytes.Length < offset) return;
            if (bytes.Length - offset < count)
                count = bytes.Length - offset;
            _inputProvider.AddSamples(bytes, offset, count);
            buildFrames();
        }
        catch
        {
        }
    }

    void buildFrames()
    {
        // 转换比例
        var ratio = OutputWaveFormat.SampleRate * OutputWaveFormat.BitsPerSample * OutputWaveFormat.Channels / (float)(_inputWaveFormat.SampleRate * _inputWaveFormat.BitsPerSample * _inputWaveFormat.Channels);
        var outputbufferSize = (int)(_inputProvider.BufferedBytes * ratio);
        if (outputbufferSize < bytesPerPacket)
        {
            return; // 不足一个包
        }
        // 确保输出缓冲区大小是包大小的整数倍
        outputbufferSize = outputbufferSize - (outputbufferSize % bytesPerPacket);
        var outputBuffer = new byte[outputbufferSize];
        var len = _outputProvider.Read(outputBuffer, 0, outputbufferSize);
        int offset = 0;
        while (offset < len)
        {
            var frame = new byte[bytesPerPacket];
            if (len - offset < bytesPerPacket)
            {
                Array.Copy(outputBuffer, offset, frame, 0, len - offset);
                _packets.Enqueue(frame);
                break; // 不足一个包，直接写入
            }
            Array.Copy(outputBuffer, offset, frame, 0, bytesPerPacket);
            _packets.Enqueue(frame);
            offset += bytesPerPacket;
        }
    }


    internal void SendRtp()
    {
        try
        {
            if (_sendFrameCount * PTimμs >= 10_000_000)
            {
                _sendFrameCount = 0;
                SendSdp();
            }  
            // 从转换器获取RTP包并发送
            if (currentPacket == null)
            {
                var flag = _packets.TryDequeue(out var packet);
                if (flag && packet != null && packet.Length > 0)
                {
                    currentPacket = packet;
                }
                else
                {
                    // 队列空且长时间无包：及时对时，避免恢复发送时时间基准滞后
                    if ((DateTime.UtcNow - _lastSendTime).TotalMilliseconds > IdleSyncThresholdMs)
                    {
                        _rtpConverter.ResetTimestamp();
                        _lastSendTime = DateTime.UtcNow;
                    }
                }
            }
            if (currentPacket != null)
            {
                if (!_rtpConverter.EnsureTime()) return;
                var rtpFrame = _rtpConverter.BuildRtpPacket(currentPacket, 0, currentPacket.Length);
                if (rtpFrame == null) return;
                currentPacket = null;
                foreach (var address in _udpClients.Keys)
                {
                    _udpClients[address].SendAsync(rtpFrame, MulticastEndpoint);
                }
                _sendFrameCount++;
                _lastSendTime = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {

        }
    }
    public void Stop()
    {
        SendSdp(true);
    }
    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        foreach (var item in _udpClients.Values)
        {
            item.Close();
        }
        _udpClients.Clear();
        _cts.Dispose();
    }
}