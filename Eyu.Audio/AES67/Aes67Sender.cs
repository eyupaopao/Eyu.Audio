namespace Eyu.Audio.AES67;

using Eyu.Audio.Timer;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Silk.NET.SDL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

public class Aes67Manager
{

    public Dictionary<string, Sdp> ExistAes67Sdp = new();
    private readonly List<IPAddress> localEndPoints;
    private readonly List<UdpClient> _sdpFinder = new();
    private readonly List<Aes67Channel> _channels = new();
    private CancellationTokenSource cts = new();
    private HighPrecisionTimer? HighPrecisionTimer;
    public Aes67Manager(params IPAddress[] localAddress)
    {
        this.localEndPoints = [.. localAddress];
        ConfigureSdpFinder();
    }
    private void ConfigureSdpFinder()
    {
        if (localEndPoints.Count == 0)
        {
            return;
        }
        foreach (var address in localEndPoints)
        {
            ConfigUdpClient(address);
        }
    }
    void ConfigUdpClient(IPAddress address)
    {
        var udpClient = new UdpClient(AddressFamily.InterNetwork);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (_sdpFinder.Count != 0) { return; }
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udpClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, address.GetAddressBytes());
            udpClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(Aes67Const.SdpMuticastIPEndPoint.Address, IPAddress.Any));
            udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, Aes67Const.SdpMuticastPort));

        }
        else
        {

            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udpClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 16);
            udpClient.JoinMulticastGroup(Aes67Const.SdpMuticastIPEndPoint.Address);
        }
        udpClient.Client.Bind(new IPEndPoint(address, Aes67Const.SdpMuticastPort));
        BeginRecive(udpClient);

        _sdpFinder.Add(udpClient);
    }
    void BeginRecive(UdpClient udpClient)
    {
        _ = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    var result = await udpClient.ReceiveAsync();
                    if (result.RemoteEndPoint.Equals(udpClient.Client.LocalEndPoint as IPEndPoint))
                        continue;
                    var sdp = new Sdp(result.Buffer);
                    if (Debugger.IsAttached)
                        Console.WriteLine(sdp);
                    var key = $"{sdp.SessId}{sdp.Crc16}";
                    if (sdp.SapMessage == Aes67Const.Deletion && ExistAes67Sdp.TryGetValue(key, out var exist))
                    {
                        ExistAes67Sdp.Remove(key);
                    }
                    else
                        ExistAes67Sdp[key] = sdp;
                }
                catch (Exception ex)
                {
                }
            }
        });
    }


    public void StopChannel()
    {

    }

    private void HandleAes67BroadCast()
    {
        if (!_channels.Any() && HighPrecisionTimer != null)
        {
            HighPrecisionTimer.Stop();
            HighPrecisionTimer.Dispose();
            HighPrecisionTimer = null;
            return;
        }
        foreach (var channel in _channels)
        {
            channel.SendRtp();
            channel.SendSdp();
        }
    }
    public void StartAes67BroadCast(ISampleProvider sampleProvider, string name, string localAddress)
    {
        var waveFormat = new WaveFormat(Aes67Const.DefaultSampleRate, Aes67Const.DefaultBitsPerSample, sampleProvider.WaveFormat.Channels);
        IWaveProvider waveProvider = null!;
        switch (Aes67Const.DefaultBitsPerSample)
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
                throw new ArgumentException($"不支持的编码格式：{Aes67Const.DefaultBitsPerSample}");
        }
        var random = new Random();
        uint ssrc = 0;
        while (true)
        {
            byte[] ssrcBytes = new byte[4];
            random.NextBytes(ssrcBytes);
            ssrc = BitConverter.ToUInt32(ssrcBytes, 0);
            if (ExistAes67Sdp.Values.Any(s => s.SessId == ssrc) || _channels.Any(c => c.SessId == ssrc)) continue;
            else break;
        }
        byte[] addressByte = [239, 69, .. IPAddress.Parse(localAddress).GetAddressBytes().SkipLast(2)];
        while (true)
        {
            var muticastAddres = new IPAddress(addressByte);
            if (ExistAes67Sdp.Values.Any(s => s.SourceIPAddress.Equals(muticastAddres.ToString())) || _channels.Any(c => c.MuticastAddress.Equals(muticastAddres.ToString())))
            {
                addressByte[2]++;
                if (addressByte[2] > 255) addressByte[2] = 0;
                continue;
            }
            break;
        }
        var channle = new Aes67Channel(waveProvider,
                                       PTPClient.Instance,
                                       Aes67Const.DefaultPTimeμs,
                                       name,
                                       ssrc,
                                       localAddress,
                                       new IPAddress(addressByte).ToString(),
                                       Aes67Const.Aes67MuticastPort,
                                       Aes67Const.DefaultEncoding,
                                       null);
        if (HighPrecisionTimer != null)
        {
            HighPrecisionTimer = new(HandleAes67BroadCast);
            HighPrecisionTimer.SetPeriod(0.25);
            HighPrecisionTimer.Start();
        }
    }
}

/// <summary>
/// AES67广播发送器，遵循AES67标准实现专业音频流的RTP广播
/// </summary>
public class Aes67Channel : IDisposable
{
    #region properties

    private readonly int _samplesPerPacket;
    private readonly Sdp _sdp;
    private readonly UdpClient _udpClient;
    private readonly PcmToRtpConverter _rtpConverter;
    private readonly IPEndPoint _multicastEndpoint;
    // 包间隔(μs)推荐包间隔：48k 125μs,250μs,333μs; 96k:250μs,1000μs,4000μs
    private readonly uint _pTimeμs;
    private readonly uint _sendFrameCount;
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();

    public uint SessId { get; }
    public string MuticastAddress { get; }
    #endregion
    /// <summary>
    /// 构造AES67广播发送器
    /// </summary>
    /// <param name="sdp">SDP会话描述</param>
    public Aes67Channel(IWaveProvider waveProvider,
                            PTPClient pTPClient,
                            uint pTimeμs,
                            string name,
                            uint sessId,
                            string localAddress,
                            string muticastAddress,
                            int muticastPort,
                            string encoding = "L24",
                            string? info = null)
    {
        _samplesPerPacket = (int)Math.Ceiling(waveProvider.WaveFormat.SampleRate * pTimeμs / 10000000f);
        _sdp = new Sdp(name,
                       sessId,
                       localAddress,
                       muticastAddress,
                       muticastPort,
                       pTPClient.PtpMaster,
                       pTPClient.Domain,
                       pTimeμs / 1000f,
                       _samplesPerPacket,
                       Aes67Const.DefaultPayloadType,
                       encoding,
                       waveProvider.WaveFormat.SampleRate,
                       waveProvider.WaveFormat.Channels,
                       info);

        if (waveProvider == null) throw new ArgumentNullException(nameof(waveProvider));
        _pTimeμs = pTimeμs;
        SessId = sessId;
        MuticastAddress = muticastAddress;
        // 初始化多播端点
        _multicastEndpoint = new IPEndPoint(IPAddress.Parse(_sdp.MuticastAddress), _sdp.MuticastPort);
        // 验证AES67所需的音频参数
        ValidateAes67Parameters();
        // 初始化UDP客户端并配置多播选项
        _udpClient = new UdpClient();
        ConfigureUdpClient();
        // 初始化RTP转换器
        _rtpConverter = new PcmToRtpConverter(
            payloadType: (byte)Aes67Const.DefaultPayloadType,
            waveProvider: waveProvider,
            pTPClient: pTPClient,
            ssrc: _sdp.SessId,
            samplesPerPacket: _samplesPerPacket
        );

    }


    /// <summary>
    /// 配置UDP客户端的多播选项
    /// </summary>
    private void ConfigureUdpClient()
    {
        // 设置多播TTL
        _udpClient.Ttl = 16;

        // 加入多播组
        //var multicastAddress = IPAddress.Parse(_sdp.MuticastAddress);
        //_udpClient.JoinMulticastGroup(multicastAddress);

        // 如果指定了本地地址，绑定到该地址
        if (!string.IsNullOrEmpty(_sdp.SourceIPAddress) && IPAddress.TryParse(_sdp.SourceIPAddress, out var localAddr))
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
        if (_sdp.PTimems <= 0 || _sdp.PTimems > 100)
        {
            throw new ArgumentException($"ptime值超出AES67推荐范围 (0.125-100ms): {_sdp.PTimems}");
        }
    }
    public void SendSdp()
    {
        _udpClient.SendAsync(_sdp.SapBytes, Aes67Const.SdpMuticastIPEndPoint);
    }

    public void SendRtp()
    {
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
        }
    }
    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _cts.Dispose();
        _udpClient?.DropMulticastGroup(IPAddress.Parse(_sdp.MuticastAddress));
        _udpClient?.Dispose();
    }
}