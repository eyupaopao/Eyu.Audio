using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Sample;

/// <summary>
/// RTP 接收监控测试：加入多播组接收 RTP 包，检测乱序与丢包。
/// 运行前需先启动发送端（如 Aes67FileBroadcastTest），或指定已存在的多播地址/端口。
/// </summary>
public static class Aes67RtpReceiveMonitorTest
{
    /// <summary>
    /// 启动 RTP 接收监控。按 Ctrl+C 退出。
    /// </summary>
    /// <param name="multicastAddress">多播地址，null 时使用 239.69.1.1</param>
    /// <param name="port">端口，0 时使用 5004</param>
    /// <param name="localAddress">本机绑定地址，null 时自动选第一个非回环 IPv4</param>
    /// <summary>
    /// 同步封装，内部调用 RunAsync。
    /// </summary>
    public static void Run(string? multicastAddress = null, int port = 0, IPAddress? localAddress = null)
    {
        RunAsync(multicastAddress, port, localAddress).GetAwaiter().GetResult();
    }

    public static async Task RunAsync(
        string? multicastAddress = null,
        int port = 0,
        IPAddress? localAddress = null)
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

        Console.WriteLine($"RTP 接收监控");
        Console.WriteLine($"  本机地址: {localAddress}");
        Console.WriteLine($"  多播: {multicastAddress}:{port}");
        Console.WriteLine($"  按 Ctrl+C 退出");
        Console.WriteLine();

        var monitor = new RtpMonitor();
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

            _ = Task.Run(() => PrintStatsLoop(monitor, cts.Token), cts.Token);

            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    var result = await udp.ReceiveAsync(cts.Token);
                    monitor.ProcessPacket(result.Buffer);
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

    static async Task PrintStatsLoop(RtpMonitor monitor, CancellationToken ct)
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
        }
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
