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
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
namespace Eyu.Audio.Aes67;

public class Aes67ChannelManager
{
    public Dictionary<string, Sdp> ExistAes67Sdp = new();
    private readonly List<IPAddress> localAddresses = new();
    private readonly List<UdpClient> _sdpFinder = new();
    private readonly List<Aes67Channel> _channels = new();
    private CancellationTokenSource cts = new();
    private HighPrecisionTimer? highPrecisionTimer;
    public static Aes67ChannelManager Inctance;
    public static void Start(params IPAddress[] localAddress)
    {
        Inctance = new Aes67ChannelManager(localAddress);
    }
    public Aes67ChannelManager(params IPAddress[] localAddress)
    {
        foreach (var item in localAddress)
        {
            if (item.AddressFamily == AddressFamily.InterNetworkV6) continue;
            this.localAddresses.Add(item);
        }
        PTPClient.Instance.Start();
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

    public void SetDefaultDefaultPTimeμs(uint pTimeμs)
    {
        if (!Aes67Const.SupportedPTimeμs.Contains(pTimeμs))
        {
            foreach (var time in Aes67Const.SupportedPTimeμs)
            {
                if (time - pTimeμs > 0)
                {
                    pTimeμs = time;
                    break;
                }
            }
        }
        Aes67Const.DefaultPTimeμs = pTimeμs;
    }
    public void SetDefaultBitsPerSample(int bitsPerSample)
    {
        if (!Aes67Const.SupportedSampleRates.Contains(bitsPerSample))
            throw new NotSupportedException("不支持的采样率");
        Aes67Const.DefaultBitsPerSample = bitsPerSample;
    }

    public void StopChannel(Aes67Channel channel)
    {
        var key = $"{channel.SessId}{channel.Sdps.Values.First().Crc16}";
        if (ExistAes67Sdp.ContainsKey(key))
        {
            ExistAes67Sdp.Remove(key);
        }
        if (channel != null)
        {
            timmerTick -= channel.SendRtp;
            channel.Stop();
            channel.Dispose();
            _channels.Remove(channel);
        }
    }
    private event Action timmerTick;
    private void HandleAes67BroadCast()
    {
        timmerTick?.Invoke();
    }

    public Aes67Channel StartAes67MulticastCast(WaveFormat inputWaveFormat, string name)
    {
        var waveFormat = WaveFormat.CreateCustomFormat(WaveFormatEncoding.Pcm, Aes67Const.DefaultSampleRate, inputWaveFormat.Channels, inputWaveFormat.AverageBytesPerSecond, inputWaveFormat.BlockAlign, Aes67Const.DefaultBitsPerSample);
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
        var channle = new Aes67Channel(
            inputWaveFormat,
            waveFormat,
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
        if (highPrecisionTimer == null)
        {
            highPrecisionTimer = new(HandleAes67BroadCast);
            highPrecisionTimer.SetPeriod(Aes67Const.DefaultPTimeμs / 1000f);
            highPrecisionTimer.Start();
        }
        //StartSendThread();
        timmerTick += channle.SendRtp;
        return channle;
    }
}
