using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using NAudio.Wave;

namespace Sample;

/// <summary>
/// RTP 接收监控测试：加入多播组接收 RTP 包，检测乱序与丢包，可选解包播放。
/// 运行前需先启动发送端（如 Aes67FileBroadcastTest），或指定已存在的多播地址/端口。
/// </summary>
public static class Aes67RtpReceiveMonitorTest
{
    /// <summary>
    /// 同步封装，内部调用 RunAsync。
    /// </summary>
    public static void Run(
        string? multicastAddress = null,
        int port = 0,
        IPAddress? localAddress = null,
        bool playback = false,
        int sampleRate = 48000,
        int bitsPerSample = 24,
        int channels = 2)
    {
        RunAsync(multicastAddress, port, localAddress, playback, sampleRate, bitsPerSample, channels)
            .GetAwaiter().GetResult();
    }

    public static async Task RunAsync(
        string? multicastAddress = null,
        int port = 0,
        IPAddress? localAddress = null,
        bool playback = false,
        int sampleRate = 48000,
        int bitsPerSample = 24,
        int channels = 2)
    {
        multicastAddress ??= "239.69.1.1";
        if (port <= 0) port = 5004;

        localAddress ??= Aes67FileBroadcastTest.GetLocalIPv4List()
            .Select(a => IPAddress.Parse(a))
            .FirstOrDefault();
        if (localAddress == null)
        {
            Console.WriteLine("错误：未找到可用的本机 IPv4 地址。");
            return;
        }

        var mcastAddr = IPAddress.Parse(multicastAddress);
        if (mcastAddr.AddressFamily != AddressFamily.InterNetwork)
        {
            Console.WriteLine("错误：目前仅支持 IPv4 多播。");
            return;
        }

        Console.WriteLine($"RTP 接收监控{(playback ? " + 播放" : "")}");
        Console.WriteLine($"  本机地址: {localAddress}");
        Console.WriteLine($"  多播: {multicastAddress}:{port}");
        if (playback)
            Console.WriteLine($"  音频格式: {sampleRate} Hz, {bitsPerSample} bit, {channels} ch");
        Console.WriteLine($"  按 Ctrl+C 退出");
        Console.WriteLine();

        var monitor = new RtpMonitor();
        using var player = playback ? new RtpPlayer(sampleRate, bitsPerSample, channels) : null;
        using var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            using var udp = new UdpClient(AddressFamily.InterNetwork);
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 16);
                udp.JoinMulticastGroup(mcastAddr);
            }
            else
            {
                udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, localAddress.GetAddressBytes());
                udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(mcastAddr, localAddress));
            }

            udp.Client.Bind(new IPEndPoint(localAddress, port));

            _ = Task.Run(() => PrintStatsLoop(monitor, player, cts.Token), cts.Token);

            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    var result = await udp.ReceiveAsync(cts.Token);
                    monitor.ProcessPacket(result.Buffer);
                    player?.ProcessPacket(result.Buffer);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"错误: {ex.Message}");
        }

        Console.WriteLine();
        monitor.PrintFinalReport();
    }

    static async Task PrintStatsLoop(RtpMonitor monitor, RtpPlayer? player, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(2000, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            monitor.PrintPeriodicReport();
            player?.PrintBufferStatus();
        }
    }
}

/// <summary>
/// RTP 解包播放器：从 RTP 包中提取 PCM 负载，大小端转换后通过 NAudio WaveOutEvent 播放。
/// AES67 规定 PCM 为网络字节序（大端），NAudio 需要小端，因此对每个样本做字节翻转。
/// </summary>
class RtpPlayer : IDisposable
{
    private readonly BufferedWaveProvider _buffer;
    private readonly WaveOutEvent _waveOut;
    private readonly int _bytesPerSample;

    public RtpPlayer(int sampleRate, int bitsPerSample, int channels)
    {
        var format = new WaveFormat(sampleRate, bitsPerSample, channels);
        _bytesPerSample = bitsPerSample / 8;

        _buffer = new BufferedWaveProvider(format)
        {
            BufferDuration = TimeSpan.FromSeconds(2),
            DiscardOnBufferOverflow = true,
            ReadFully = true
        };

        _waveOut = new WaveOutEvent { DesiredLatency = 200 };
        _waveOut.Init(_buffer);
        _waveOut.Play();
        Console.WriteLine("播放器已启动，等待 RTP 数据...");
    }

    /// <summary>
    /// 解包单个 RTP 数据包，提取 PCM 负载并送入播放缓冲区。
    /// </summary>
    public void ProcessPacket(byte[] data)
    {
        if (data == null || data.Length < 12) return;

        int cc = data[0] & 0x0F;
        int headerSize = 12 + cc * 4;

        bool hasExtension = (data[0] & 0x10) != 0;
        if (hasExtension)
        {
            if (data.Length < headerSize + 4) return;
            int extLength = (data[headerSize + 2] << 8) | data[headerSize + 3];
            headerSize += 4 + extLength * 4;
        }

        if (data.Length <= headerSize) return;

        int payloadLength = data.Length - headerSize;
        // 对齐到完整样本
        payloadLength -= payloadLength % _bytesPerSample;
        if (payloadLength <= 0) return;

        byte[] payload = new byte[payloadLength];
        Buffer.BlockCopy(data, headerSize, payload, 0, payloadLength);

        SwapEndianness(payload, _bytesPerSample);
        _buffer.AddSamples(payload, 0, payloadLength);
    }

    public void PrintBufferStatus()
    {
        var buffered = _buffer.BufferedDuration;
        Console.WriteLine($"  播放缓冲: {buffered.TotalMilliseconds:F0} ms");
    }

    private static void SwapEndianness(byte[] data, int bytesPerSample)
    {
        for (int i = 0; i <= data.Length - bytesPerSample; i += bytesPerSample)
        {
            switch (bytesPerSample)
            {
                case 2:
                    (data[i], data[i + 1]) = (data[i + 1], data[i]);
                    break;
                case 3:
                    (data[i], data[i + 2]) = (data[i + 2], data[i]);
                    break;
                case 4:
                    (data[i], data[i + 3]) = (data[i + 3], data[i]);
                    (data[i + 1], data[i + 2]) = (data[i + 2], data[i + 1]);
                    break;
            }
        }
    }

    public void Dispose()
    {
        _waveOut.Stop();
        _waveOut.Dispose();
    }
}

/// <summary>
/// 按 SSRC 跟踪 RTP 包，检测丢包与乱序。
/// </summary>
class RtpMonitor
{
    const int MinRtpHeaderSize = 12;

    class StreamState
    {
        public ushort LastSeq;
        public bool Initialized;
        public long TotalReceived;
        public long Duplicates;
        public long OutOfOrder;
        public long Lost;
        public uint LastTimestamp;
    }

    readonly ConcurrentDictionary<uint, StreamState> _streams = new();
    readonly object _lock = new();
    long _totalPackets;
    DateTime _startTime = DateTime.UtcNow;

    public void ProcessPacket(byte[] data)
    {
        if (data == null || data.Length < MinRtpHeaderSize)
            return;

        // RTP 头：seq@2-3, timestamp@4-7, ssrc@8-11 (大端)
        ushort seq = (ushort)((data[2] << 8) | data[3]);
        uint timestamp = (uint)((data[4] << 24) | (data[5] << 16) | (data[6] << 8) | data[7]);
        uint ssrc = (uint)((data[8] << 24) | (data[9] << 16) | (data[10] << 8) | data[11]);

        var s = _streams.GetOrAdd(ssrc, _ => new StreamState());
        lock (_lock)
        {
            _totalPackets++;
            s.TotalReceived++;

            if (!s.Initialized)
            {
                s.LastSeq = seq;
                s.LastTimestamp = timestamp;
                s.Initialized = true;
                return;
            }

            // 有符号差值，正确处理 16 位回绕
            int delta = (short)(seq - s.LastSeq);

            if (delta == 0)
            {
                s.Duplicates++;
                return;
            }

            if (delta < 0)
            {
                s.OutOfOrder++;
                return;
            }

            // delta > 0
            if (delta > 1)
            {
                s.Lost += delta - 1;
            }
            s.LastSeq = seq;
            s.LastTimestamp = timestamp;
        }
    }

    public void PrintPeriodicReport()
    {
        lock (_lock)
        {
            if (_streams.Count == 0) return;

            var elapsed = (DateTime.UtcNow - _startTime).TotalSeconds;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 运行 {elapsed:F0}s | 总包: {_totalPackets}");

            foreach (var (ssrc, s) in _streams)
            {
                if (s.TotalReceived == 0) continue;
                var issues = new List<string>();
                if (s.Lost > 0) issues.Add($"丢包 {s.Lost}");
                if (s.OutOfOrder > 0) issues.Add($"乱序 {s.OutOfOrder}");
                if (s.Duplicates > 0) issues.Add($"重复 {s.Duplicates}");
                var status = issues.Count > 0 ? $" | {string.Join(", ", issues)}" : " | 正常";
                Console.WriteLine($"  SSRC 0x{ssrc:X8}: 收到 {s.TotalReceived}{status}");
            }
            Console.WriteLine();
        }
    }

    public void PrintFinalReport()
    {
        lock (_lock)
        {
            var elapsed = (DateTime.UtcNow - _startTime).TotalSeconds;
            Console.WriteLine("========== RTP 监控结果 ==========");
            Console.WriteLine($"运行时长: {elapsed:F1} 秒");
            Console.WriteLine($"总包数: {_totalPackets}");

            foreach (var (ssrc, s) in _streams)
            {
                Console.WriteLine($"  SSRC 0x{ssrc:X8}:");
                Console.WriteLine($"    收到: {s.TotalReceived}");
                Console.WriteLine($"    丢包: {s.Lost}");
                Console.WriteLine($"    乱序: {s.OutOfOrder}");
                Console.WriteLine($"    重复: {s.Duplicates}");
            }
            Console.WriteLine("====================================");
        }
    }
}
