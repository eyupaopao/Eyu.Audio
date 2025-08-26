using Eyu.Audio.Provider;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using static Eyu.Audio.Aes67.Aes67Const;

namespace Eyu.Audio.Aes67;

/// <summary>
/// AES67广播发送器，遵循AES67标准实现专业音频流的RTP广播
/// </summary>
public class Aes67Channel : IDisposable
{
    #region properties

    private readonly int _samplesPerPacket;
    public readonly Dictionary<IPAddress, Sdp> Sdps = new();
    private readonly Dictionary<IPAddress, UdpClient> _udpClients = new();
    private readonly PcmToRtpConverter _rtpConverter;
    private readonly IPEndPoint _multicastEndpoint;
    private WaveFormat _inputWaveFormat = null!;
    private readonly WaveFormat _outputWaveFormat;

    // 包间隔(μs)推荐包间隔：48k 125μs,250μs,333μs; 96k:250μs,1000μs,4000μs
    private uint _sendFrameCount;
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();
    private readonly List<IPAddress> localAddresses;
    BufferedWaveProvider _inputProvider;
    IWaveProvider _outputProvider;
    public uint SessId { get; }
    public IPAddress MuticastAddress { get; }
    #endregion
    /// <summary>
    /// 构造AES67广播发送器
    /// </summary>
    /// <param name="sdp">SDP会话描述</param>
    public Aes67Channel(uint sessId, List<IPAddress> localAddresses, IPAddress muticastAddress, int muticastPort, string name, string? info = null)
    {
        // 强制统一输出格式。
        _outputWaveFormat = new WaveFormat(DefaultSampleRate, DefaultBitsPerSample, DefaultChannels);
        _samplesPerPacket = (int)Math.Ceiling(DefaultSampleRate * DefaultPTimeμs / 1000000f);
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
                       DefaultPTimeμs / 1000f,
                       _samplesPerPacket,
                       DefaultEncoding,
                       DefaultSampleRate,
                       DefaultChannels,
                       0,
                       info);
            ValidateAes67Parameters(sdp);
            var udpClient = new UdpClient(new IPEndPoint(address, 0));
            udpClient.Ttl = 16;
            _udpClients[address] = udpClient;
            Sdps[address] = sdp;
        }
        SessId = sessId;
        this.localAddresses = localAddresses;
        MuticastAddress = muticastAddress;
        // 初始化多播端点
        _multicastEndpoint = new IPEndPoint(MuticastAddress, muticastPort);
        // 初始化RTP转换器
        _rtpConverter = new PcmToRtpConverter(
            pTPClient, DefaultSampleRate, DefaultBitsPerSample, DefaultChannels,
            (byte)DefaultPayloadType,
            _samplesPerPacket
        );
    }

    internal void Init(WaveFormat inputWaveFormat, string name)
    {
        if (_inputWaveFormat == null || !_inputWaveFormat.Equals(inputWaveFormat))
        {
            _inputWaveFormat = inputWaveFormat;
            BuildProvider();
        }
        if (!string.IsNullOrEmpty(name))
        {
            foreach (var sdp in Sdps.Values)
            {
                sdp.SetName(name);
            }
        }
        _rtpConverter.Initialize();
        SendSdp();
    }
    void BuildProvider()
    {
        _inputProvider = new BufferedWaveProvider(_inputWaveFormat);
        IWaveProvider waveProvider = _inputProvider;
        if (_inputWaveFormat.Channels == 1 && DefaultChannels == 2)
        {
            waveProvider = new StereoToMonoProvider16(waveProvider);
        }
        if (_inputWaveFormat.SampleRate == _outputWaveFormat.SampleRate && _inputWaveFormat.BitsPerSample == _outputWaveFormat.BitsPerSample)
        {
            _outputProvider = waveProvider;
            return;
        }
        ISampleProvider sampleProvider = new SampleChannel(waveProvider, false);
        var waveFormat = new WaveFormat(DefaultSampleRate, DefaultBitsPerSample, sampleProvider.WaveFormat.Channels);
        // 采样率不一样的需要重采样。
        if (waveProvider.WaveFormat.SampleRate != DefaultSampleRate)
            sampleProvider = new SampleWaveFormatConversionProvider(new WaveFormat(DefaultSampleRate, waveProvider.WaveFormat.Channels), sampleProvider);
        // 位深不一样的，需要转换位深。
        switch (DefaultBitsPerSample)
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
                throw new ArgumentException($"不支持的编码格式：{DefaultBitsPerSample}");
        }
        bytesPerPacket = _samplesPerPacket * waveFormat.Channels * (waveFormat.BitsPerSample / 8);
    }

    /// <summary>
    /// 验证AES67所需的参数
    /// </summary>
    private void ValidateAes67Parameters(Sdp sdp)
    {
        if (!Array.Exists(SupportedSampleRates, rate => rate == sdp.SampleRate))
        {
            throw new ArgumentException($"AES67不支持的采样率: {sdp.SampleRate}. 支持的采样率: {string.Join(", ", SupportedSampleRates)}");
        }

        // AES67要求支持的编码: L16, L24
        if (sdp.AudioEncoding != "L16" && sdp.AudioEncoding != "L24")
        {
            throw new ArgumentException($"AES67不支持的编码格式: {sdp.AudioEncoding}. 仅支持L16和L24");
        }

        // 验证ptime (AES67通常使用0.125ms到100ms)
        if (sdp.PTimems <= 0 || sdp.PTimems > 100)
        {
            throw new ArgumentException($"ptime值超出AES67推荐范围 (0.125-100ms): {sdp.PTimems}");
        }
    }
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

    public void ChangeName(string name)
    {
        foreach (var sdp in Sdps.Values)
        {
            sdp.SetName(name);
        }
    }
    void buildFrames()
    {
        // 转换比例
        var ratio = _outputWaveFormat.SampleRate * _outputWaveFormat.BitsPerSample * _outputWaveFormat.Channels / (float)(_inputWaveFormat.SampleRate * _inputWaveFormat.BitsPerSample * _inputWaveFormat.Channels);
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
                _packets.Writer.TryWrite(frame);
                break; // 不足一个包，直接写入
            }
            Array.Copy(outputBuffer, offset, frame, 0, bytesPerPacket);
            _packets.Writer.TryWrite(frame);
            offset += bytesPerPacket;
        }
    }

    Channel<byte[]> _packets = Channel.CreateUnbounded<byte[]>();
    private int bytesPerPacket;
    byte[]? currentPacket = null!;
    private void SendSdp(bool deletion = false)
    {
        foreach (var address in _udpClients.Keys)
        {
            if (Sdps.TryGetValue(address, out var sdp))
            {
                _udpClients[address].SendAsync(sdp.BuildSap(deletion), SdpMulticastIPEndPoint);
            }
        }
    }
    public void SendRtp()
    {
        try
        {
            if (_sendFrameCount * DefaultPTimeμs >= 10_000_000)
            {
                _sendFrameCount = 0;
                SendSdp();
            }

            // 从转换器获取RTP包并发送
            if (currentPacket == null)
            {
                var flag = _packets.Reader.TryRead(out var packet);
                if (flag && packet != null && packet.Length > 0)
                {
                    currentPacket = packet;
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
                    _udpClients[address].SendAsync(rtpFrame, _multicastEndpoint);
                }
                _sendFrameCount++;
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