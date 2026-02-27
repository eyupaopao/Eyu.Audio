using Eyu.Audio.Provider;
using Eyu.Audio.PTP;
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
/// <summary>
/// aes67 广播管理器
/// </summary>
public class Aes67ChannelManager
{
    private Dictionary<string, Sdp> _existAes67Sdp = new();
    private readonly List<IPAddress> localAddresses = new();
    private readonly List<UdpClient> _sdpFinder = new();
    private readonly List<Aes67Channel> _channels = new();
    public static Action<Sdp, bool>? SdpOnlineEvent;
    private CancellationTokenSource cts;
    private HighPrecisionTimer? highPrecisionTimer;
    private event Action? timmerTick;
    public static Aes67ChannelManager Instance = null!;
    public uint PTimeμs { get; private set; } = Aes67Const.DefaultPTimeμs;
    /// <summary>
    /// Initializes and starts the AES67 channel manager and associated PTP client using the specified local IP
    /// addresses.
    /// </summary>
    /// <remarks>This method must be called before using any channel management features. Calling this method
    /// multiple times will reinitialize the channel manager and restart the PTP client.</remarks>
    /// <param name="localAddress">An array of local IP addresses to bind the channel manager to. At least one address should be provided to enable
    /// proper network operation.</param>
    public static void Start(params IPAddress[] localAddress)
    {
        Instance = new Aes67ChannelManager(localAddress);
        PTPClient.Instance.Start();
        Instance.cts = new();
        Instance.ConfigureSdpFinder();
    }
    /// <summary>
    /// Stops the current instance and releases all associated resources.
    /// </summary>
    /// <remarks>This method cancels ongoing operations, stops all active channels, and disposes of network
    /// resources. After calling this method, the instance is no longer usable and must be reinitialized before further
    /// use.</remarks>
    public static void Stop()
    {
        if (Instance == null) return;
        PTPClient.Instance.Stop();
        Instance.cts.Cancel();
        foreach (var udp in Instance._sdpFinder)
        {
            try
            {
                udp.DropMulticastGroup(Aes67Const.SdpMulticastIPEndPoint.Address);
            }
            catch { }
            udp.Close();
            udp.Dispose();
        }
        Instance._sdpFinder.Clear();
        foreach (var channel in Instance._channels)
        {
            channel.Stop();
        }
        Instance.highPrecisionTimer?.Stop();
        Instance.highPrecisionTimer = null;
        Instance = null!;
    }
    /// <summary>
    /// Stops the specified AES67 channel and releases its associated resources.
    /// </summary>
    /// <remarks>If this is the last active channel, the underlying high-precision timer is also stopped and
    /// released. After calling this method, the specified channel should not be used.</remarks>
    /// <param name="channel">The AES67 channel to stop and dispose. If null, the method performs no action.</param>
    internal void StopChannel(Aes67Channel channel)
    {
        if (channel == null) return;
        var key = $"{channel.SessId}{channel.Sdps.Values.First().MessageHash}";
        if (_existAes67Sdp.ContainsKey(key))
        {
            _existAes67Sdp.Remove(key);
        }
        if (channel != null)
        {
            timmerTick -= channel.SendRtp;
            _channels.Remove(channel);
        }
        if (_channels.Count == 0 && highPrecisionTimer != null)
        {
            highPrecisionTimer?.Stop();
            highPrecisionTimer = null;
        }
    }
    Random random = new Random();
    /// <summary>
    /// Retrieves all currently active SDP entries, optionally filtered by device identifier.
    /// </summary>
    /// <remarks>Only SDP entries considered active within the last 20 seconds are returned. This method
    /// yields results as they are found and may be used in a foreach loop for efficient enumeration.</remarks>
    /// <param name="devId">The device identifier to filter the SDP entries. If null, returns entries for all devices.</param>
    /// <returns>An enumerable collection of SDP entries that have been received within the last 20 seconds. The collection is
    /// filtered by device identifier if specified.</returns>
    public IEnumerable<Sdp> GetExistSdps(string? devId = null)
    {
        foreach (var item in _existAes67Sdp.Values)
        {
            if ((DateTime.Now - item.LastReciveTime) < TimeSpan.FromSeconds(20))
            {
                if (devId == null)
                    yield return item;
                else if (item.DevId == devId) yield return item;
            }
        }
    }
    /// <summary>
    /// Retrieves the existing SDP associated with the specified key, if present.
    /// </summary>
    /// <param name="key">The key used to locate the corresponding SDP. Cannot be null.</param>
    /// <returns>The existing SDP associated with the specified key, or null if no such entry exists.</returns>
    public Sdp? GetExistSdp(string key)
    {
        var flag = _existAes67Sdp.TryGetValue(key, out var value);
        return value;
    }
    Aes67ChannelManager(params IPAddress[] localAddress)
    {
        foreach (var item in localAddress)
        {
            if (item.AddressFamily == AddressFamily.InterNetworkV6) continue;
            this.localAddresses.Add(item);
        }
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
                    var timeout = new CancellationTokenSource(30000);
                    var result = await udpClient.ReceiveAsync(timeout.Token);
                    if (result.RemoteEndPoint.Equals(udpClient.Client.LocalEndPoint as IPEndPoint))
                        continue;
                    var sdp = new Sdp(result.Buffer);
                    if (sdp.SapMessage == Aes67Const.Deletion)
                    {
                        if (_existAes67Sdp.TryGetValue(sdp.Key, out var exist))
                            _existAes67Sdp.Remove(sdp.Key);
                        SdpOnlineEvent?.Invoke(sdp, false);
                    }
                    else
                    {
                        _existAes67Sdp[sdp.Key] = sdp;
                        SdpOnlineEvent?.Invoke(sdp, true);
                    }
                }
                catch (Exception ex)
                {
                    var timeouted = new List<Sdp>();
                    foreach (var item in _existAes67Sdp.Values)
                    {
                        if ((DateTime.Now - item.LastReciveTime) > TimeSpan.FromSeconds(20))
                            timeouted.Add(item);
                    }
                    foreach (var item in timeouted)
                    {
                        _existAes67Sdp.Remove(item.Key);
                        SdpOnlineEvent?.Invoke(item, false);
                    }
                }
            }
        });
    }
    /// <summary>
    /// Sets the default packet time, in microseconds, to the specified value or the next supported value if the
    /// specified value is not supported.
    /// </summary>
    /// <remarks>The method ensures that the packet time is set to a value supported by the system. If the
    /// specified value is not in the list of supported packet times, the next higher supported value is selected
    /// automatically.</remarks>
    /// <param name="pTimeμs">The desired packet time in microseconds. If the value is not supported, the next higher supported value is used.</param>
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
        PTimeμs = pTimeμs;
    }
    /// <summary>
    /// Sets the default bits per sample value for audio processing operations.
    /// </summary>
    /// <param name="bitsPerSample">The bits per sample value to set as the default. Must be one of the supported values defined in
    /// Aes67Const.SupportedBitsPerSample.</param>
    /// <exception cref="NotSupportedException">Thrown if bitsPerSample is not a supported value.</exception>
    public void SetDefaultBitsPerSample(int bitsPerSample)
    {
        if (!Aes67Const.SupportedBitsPerSample.Contains(bitsPerSample))
            throw new NotSupportedException("不支持的位深");
        Aes67Const.DefaultBitsPerSample = bitsPerSample;
    }
    /// <summary>
    /// Sets the default sample rate for audio processing operations.
    /// </summary>
    /// <param name="bitsPerSample">The sample rate, in bits per sample, to set as the default. Must be one of the supported sample rates.</param>
    /// <exception cref="NotSupportedException">Thrown if the specified sample rate is not supported.</exception>
    public void SetDefaultSampleRate(int bitsPerSample)
    {
        if (!Aes67Const.SupportedSampleRates.Contains(bitsPerSample))
            throw new NotSupportedException("不支持的采样率");
        Aes67Const.DefaultBitsPerSample = bitsPerSample;
    }

    private void HandleAes67BroadCast()
    {
        timmerTick?.Invoke();
    }
    private uint GenSsrc()
    {
        uint ssrc = 0;
        byte[] ssrcBytes = new byte[4];
        while (true)
        {
            random.NextBytes(ssrcBytes);
            ssrc = BitConverter.ToUInt32(ssrcBytes, 0);
            if (_existAes67Sdp.Values.Any(s => s.SessId == ssrc) || _channels.Any(c => c.SessId == ssrc)) continue;
            else break;
        }
        return ssrc;

    }
    public IPAddress GetUseAbleMcastAddress()
    {
        byte[] muticastAddressByte = [239, 69, 1, 1];
        while (true)
        {
            var multicastAddress = new IPAddress(muticastAddressByte);
            if (_existAes67Sdp.Values.Any(s => s.MuticastAddress.Equals(multicastAddress.ToString())) || _channels.Any(c => c.MuticastAddress.Equals(multicastAddress.ToString())))
            {
                muticastAddressByte[3]++;
                if (muticastAddressByte[3] > 255)
                {
                    muticastAddressByte[2]++;
                    muticastAddressByte[3] = 1;
                }
                continue;
            }
            break;
        }
        return new IPAddress(muticastAddressByte);
    }
    /// <summary>
    /// 创建广播
    /// </summary>
    /// <param name="inputWaveFormat">数据格式</param>
    /// <param name="name">广播名称</param>
    /// <param name="duration">时间长度(秒)</param>
    /// <returns></returns>
    public Aes67Channel CreateMulticastcastChannel(string name)
    {
        var channel = new Aes67Channel(
            GenSsrc(),
            localAddresses,
            GetUseAbleMcastAddress(),
            Aes67Const.Aes67MuticastPort,
            name,
            PTimeμs);
        return channel;
    }
    internal void Init(Aes67Channel channel, WaveFormat waveFormat, string name = "")
    {
        if (highPrecisionTimer == null)
        {
            highPrecisionTimer = new(HandleAes67BroadCast);
            // 设置定时器周期为默认的包间隔时间的1/10（单位：ms）
            highPrecisionTimer.SetPeriod(PTimeμs / 10000f);
            highPrecisionTimer.Start();
        }
        if (!_channels.Contains(channel))
        {
            timmerTick += channel.SendRtp;
            _channels.Add(channel);
        }
    }
}
