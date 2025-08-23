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
    private readonly uint _pTimeμs;
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
    public Aes67Channel(WaveFormat inputWaveFormat, uint sessId, List<IPAddress> localAddresses, IPAddress muticastAddress, int muticastPort, string name, int duration, string? info = null)
    {
        var waveFormat = new WaveFormat(DefaultSampleRate, DefaultBitsPerSample, inputWaveFormat.Channels);
        _samplesPerPacket = (int)Math.Ceiling(waveFormat.SampleRate * DefaultPTimeμs / 1000000f);
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
                       duration,
                       info);
            ValidateAes67Parameters(sdp);
            var udpClient = new UdpClient(new IPEndPoint(address, 0));
            udpClient.Ttl = 16;
            _udpClients[address] = udpClient;
            Sdps[address] = sdp;
        }
        this._outputWaveFormat = waveFormat;
        _pTimeμs = DefaultPTimeμs;
        _inputWaveFormat = inputWaveFormat;
        SessId = sessId;
        this.localAddresses = localAddresses;
        MuticastAddress = muticastAddress;
        // 初始化多播端点
        _multicastEndpoint = new IPEndPoint(MuticastAddress, muticastPort);
        // 初始化RTP转换器
        _rtpConverter = new PcmToRtpConverter(
            pTPClient,
            waveFormat.SampleRate, waveFormat.BitsPerSample, waveFormat.Channels,
            (byte)DefaultPayloadType,
            sessId,
            _samplesPerPacket
        );
        BuildProvider();

    }

    void BuildProvider()
    {
        _inputProvider = new BufferedWaveProvider(_inputWaveFormat);
        if (_inputWaveFormat.SampleRate == _outputWaveFormat.SampleRate && _inputWaveFormat.BitsPerSample == _outputWaveFormat.BitsPerSample)
        {
            _outputProvider = _inputProvider;
            return;
        }
        ISampleProvider sampleProvider = new SampleChannel(_inputProvider,false);
        var waveFormat = new WaveFormat(DefaultSampleRate, DefaultBitsPerSample, sampleProvider.WaveFormat.Channels);
        if (_inputProvider.WaveFormat.SampleRate != DefaultSampleRate)
            sampleProvider = new SampleWaveFormatConversionProvider(new WaveFormat(DefaultSampleRate, _inputProvider.WaveFormat.Channels), sampleProvider);
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

    public void SendRtp()
    {
        try
        {
            if (_sendFrameCount * _pTimeμs >= 10000000)
            {
                _sendFrameCount = 0;
            }
            if (_sendFrameCount == 0)
            {
                foreach (var address in _udpClients.Keys)
                {
                    _udpClients[address].SendAsync(Sdps[address].BuildSap(), SdpMulticastIPEndPoint);
                }
            }
            // 从转换器获取RTP包并发送
            var flag = _packets.Reader.TryRead(out var packet);
            if (flag && packet != null && packet.Length > 0)
            {
                var rtpFrame = _rtpConverter.BuildRtpPacket(packet, 0, packet.Length);
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
        foreach (var address in _udpClients.Keys)
        {
            _udpClients[address].SendAsync(Sdps[address].BuildSap(true), SdpMulticastIPEndPoint);
        }
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