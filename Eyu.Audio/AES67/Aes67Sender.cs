using Eyu.Audio.Provider;
using Eyu.Audio.Timer;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
namespace Eyu.Audio.Aes67;

public class Aes67AudioManager
{
    public Dictionary<string, Sdp> ExistAes67Sdp = new();
    private readonly List<IPAddress> localAddresses = new();
    private readonly List<UdpClient> _sdpFinder = new();
    private readonly List<Aes67Channel> _channels = new();
    private CancellationTokenSource cts = new();
    private HighPrecisionTimer? highPrecisionTimer;
    public static Aes67AudioManager Inctance;
    public static void Start(params IPAddress[] localAddress)
    {
        Inctance = new Aes67AudioManager(localAddress);
    }
    public Aes67AudioManager(params IPAddress[] localAddress)
    {
        foreach (var item in localAddress)
        {
            if (item.AddressFamily == AddressFamily.InterNetworkV6) continue;
            this.localAddresses.Add(item);
        }
        ConfigureSdpFinder();
    }
    private void ConfigureSdpFinder()
    {
        if (localAddresses.Count == 0)
        {
            return;
        }
        foreach (var address in localAddresses)
        {
            ConfigUdpClient(address);
        }
    }
    void ConfigUdpClient(IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetworkV6) return;
        var udpClient = new UdpClient(AddressFamily.InterNetwork);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (_sdpFinder.Count != 0) { return; }
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udpClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, address.GetAddressBytes());
            udpClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(Aes67Const.SdpMulticastIPEndPoint.Address, IPAddress.Any));
            udpClient.Client.Bind(new IPEndPoint(address, Aes67Const.SdpMulticastPort));

        }
        else
        {

            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udpClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 16);
            udpClient.JoinMulticastGroup(Aes67Const.SdpMulticastIPEndPoint.Address);
        }
        udpClient.Client.Bind(new IPEndPoint(address, Aes67Const.SdpMulticastPort));
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


    public void StopChannel(Aes67Channel channel)
    {
        var key = $"{channel.SessId}{channel.Sdps.Values.First().Crc16}";
        if (ExistAes67Sdp.ContainsKey(key))
        {
            ExistAes67Sdp.Remove(key);
            if (channel != null)
            {
                timmerTick -= channel.SendRtp;
                _channels.Remove(channel);
            }
        }
    }
    private event Action timmerTick;
    private void HandleAes67BroadCast()
    {
        if (!_channels.Any() && highPrecisionTimer != null)
        {
            highPrecisionTimer.Stop();
            highPrecisionTimer.Dispose();
            highPrecisionTimer = null;
            return;
        }
        timmerTick?.Invoke();
        timmerTick?.Invoke();
        timmerTick?.Invoke();
        timmerTick?.Invoke();
        timmerTick?.Invoke();
        timmerTick?.Invoke();
        timmerTick?.Invoke();
        timmerTick?.Invoke(); 
        timmerTick?.Invoke();
        timmerTick?.Invoke();
        timmerTick?.Invoke();
        timmerTick?.Invoke();
        timmerTick?.Invoke();
        timmerTick?.Invoke();
        timmerTick?.Invoke();
        timmerTick?.Invoke();
        timmerTick?.Invoke();
        timmerTick?.Invoke();
        timmerTick?.Invoke();
        timmerTick?.Invoke();
        timmerTick?.Invoke();
        timmerTick?.Invoke();
        timmerTick?.Invoke();
        timmerTick?.Invoke();
    }
    public Aes67Channel StartAes67MulticastCast(IWaveProvider inputWave, string name)
    {
        ISampleProvider sampleProvider = new SampleChannel(inputWave);
        var waveFormat = new WaveFormat(Aes67Const.DefaultSampleRate, Aes67Const.DefaultBitsPerSample, sampleProvider.WaveFormat.Channels);
        if (inputWave.WaveFormat.SampleRate != Aes67Const.DefaultSampleRate)
            sampleProvider = new SampleWaveFormatConversionProvider(new WaveFormat(Aes67Const.DefaultSampleRate, inputWave.WaveFormat.Channels), sampleProvider);
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
        byte[] ssrcBytes = new byte[4];
        while (true)
        {
            random.NextBytes(ssrcBytes);
            ssrc = BitConverter.ToUInt32(ssrcBytes, 0);
            if (ExistAes67Sdp.Values.Any(s => s.SessId == ssrc) || _channels.Any(c => c.SessId == ssrc)) continue;
            else break;
        }
        byte[] addressByte = [239, 69, 1, 1];
        while (true)
        {
            var muticastAddres = new IPAddress(ssrcBytes);
            if (ExistAes67Sdp.Values.Any(s => s.SourceIPAddress.Equals(muticastAddres.ToString())) || _channels.Any(c => c.MuticastAddress.Equals(muticastAddres.ToString())))
            {
                addressByte[3]++;
                if (addressByte[3] > 255)
                {
                    addressByte[2]++;
                    addressByte[3] = 1;
                }
                continue;
            }
            break;
        }
        var channle = new Aes67Channel(waveProvider,
                                       PTPClient.Instance,
                                       Aes67Const.DefaultPTimeμs,
                                       name,
                                       ssrc,
                                       localAddresses,
                                       new IPAddress(addressByte).ToString(),
                                       Aes67Const.Aes67MuticastPort,
                                       Aes67Const.DefaultEncoding,
                                       null);
        _channels.Add(channle);
        if (highPrecisionTimer != null)
        {
            highPrecisionTimer = new(HandleAes67BroadCast);
            highPrecisionTimer.SetPeriod(0.25);
            highPrecisionTimer.Start();
        }
        //StartSendThread();
        timmerTick += channle.SendRtp;
        return channle;
    }
    //bool sending;
    //void StartSendThread()
    //{
    //    if (sending) return;
    //    sending = true;
    //    new Thread(() =>
    //    {
    //        while (_channels.Any())
    //        {
    //            timmerTick?.Invoke();
    //        }
    //        sending = false;
    //    }).Start();
    //}

}

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
    // 包间隔(μs)推荐包间隔：48k 125μs,250μs,333μs; 96k:250μs,1000μs,4000μs
    private readonly uint _pTimeμs;
    private uint _sendFrameCount;
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
                            List<IPAddress> localAddresses,
                            string muticastAddress,
                            int muticastPort,
                            string encoding = "L24",
                            string? info = null)
    {
        _samplesPerPacket = (int)Math.Ceiling(waveProvider.WaveFormat.SampleRate * pTimeμs / 1000000f);
        foreach (var address in localAddresses)
        {
            var sdp = new Sdp(name,
                       sessId,
                       address.ToString(),
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
            ValidateAes67Parameters(sdp);
            var udpClient = new UdpClient(new IPEndPoint(address, 0));
            udpClient.Ttl = 16;
            _udpClients[address] = udpClient;
            Sdps[address] = sdp;
        }

        if (waveProvider == null) throw new ArgumentNullException(nameof(waveProvider));
        _pTimeμs = pTimeμs;
        SessId = sessId;
        MuticastAddress = muticastAddress;
        // 初始化多播端点
        _multicastEndpoint = new IPEndPoint(IPAddress.Parse(MuticastAddress), muticastPort);
        // 验证AES67所需的音频参数
        // 初始化UDP客户端并配置多播选项
        // 初始化RTP转换器
        _rtpConverter = new PcmToRtpConverter(
            payloadType: (byte)Aes67Const.DefaultPayloadType,
            waveProvider: waveProvider,
            pTPClient: pTPClient,
            ssrc: sessId,
            samplesPerPacket: _samplesPerPacket
        );

    }


    /// <summary>
    /// 验证AES67所需的参数
    /// </summary>
    private void ValidateAes67Parameters(Sdp sdp)
    {
        if (!Array.Exists(Aes67Const.SupportedSampleRates, rate => rate == sdp.SampleRate))
        {
            throw new ArgumentException($"AES67不支持的采样率: {sdp.SampleRate}. 支持的采样率: {string.Join(", ", Aes67Const.SupportedSampleRates)}");
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

    public void SendRtp()
    {
        try
        {
            // 从转换器获取RTP包并发送
            byte[] rtpFrame = _rtpConverter.ReadRtpFrame();
            if (rtpFrame != null && rtpFrame.Length > 0)
            {
                foreach (var address in _udpClients.Keys)
                {
                    _udpClients[address].SendAsync(rtpFrame, _multicastEndpoint);
                }
                _sendFrameCount++;
            }
            if (_sendFrameCount >= 100)
            {
                foreach (var address in _udpClients.Keys)
                {
                    _udpClients[address].SendAsync(Sdps[address].BuildSap(), Aes67Const.SdpMulticastIPEndPoint);
                }
                _sendFrameCount = 0;
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
    }
}