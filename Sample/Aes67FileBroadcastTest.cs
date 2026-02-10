using Eyu.Audio.Aes67;
using Eyu.Audio.Reader;
using Eyu.Audio.Timer;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Sample;

/// <summary>
/// 使用 AudioFileReader 读取音频文件得到 PCM，经 Aes67ChannelManager 创建 Aes67Channel，
/// 通过 Aes67Channel 广播 PCM；定时由 Aes67ChannelManager 内部的 HighPrecisionTimer 驱动。
/// </summary>
public static class Aes67FileBroadcastTest
{
    /// <summary>
    /// 从音频文件读取 PCM 并通过 AES67 多播广播。
    /// </summary>
    /// <param name="audioFilePath">音频文件路径（支持 wav/mp3/flac/ogg 等）</param>
    /// <param name="broadcastName">SDP 中的流名称</param>
    /// <param name="localAddress">本机用于发送的 IPv4 地址，null 时自动选第一个非回环地址</param>
    /// <param name="durationSeconds">广播时长（秒），≤0 表示播完文件后继续等待 5 秒再结束</param>
    public static void BroadcastFromFile(
        string audioFilePath,
        string broadcastName = "Eyu.Audio AES67 File Broadcast",
        IPAddress? localAddress = null,
        int durationSeconds = 0)
    {
        if (string.IsNullOrWhiteSpace(audioFilePath) || !File.Exists(audioFilePath))
        {
            Console.WriteLine("错误：音频文件不存在。");
            return;
        }

        localAddress ??= GetFirstLocalIPv4();
        if (localAddress == null)
        {
            Console.WriteLine("错误：未找到可用的本机 IPv4 地址。");
            return;
        }

        Console.WriteLine($"使用本机地址: {localAddress}");
        Console.WriteLine($"音频文件: {audioFilePath}");
        Console.WriteLine($"流名称: {broadcastName}");

        using var audioReader = new AudioFileReader(audioFilePath);
        if (!audioReader.CanRead)
        {
            Console.WriteLine("错误：无法读取音频文件。");
            return;
        }

        var waveFormat = audioReader.WaveFormat;
        Console.WriteLine($"格式: {waveFormat.SampleRate} Hz, {waveFormat.BitsPerSample} bit, {waveFormat.Channels} 声道");

        Aes67ChannelManager.Start(localAddress);
        try
        {
            var manager = Aes67ChannelManager.Instance;
            manager.SetDefaultDefaultPTimeμs(1000);
            var channel = manager.CreateMulticastcastChannel(broadcastName);
            manager.Init(channel, waveFormat, broadcastName);

            // 生成 stream.sdp 供 ffplay 接收测试
            var sdpPath = Path.Combine(AppContext.BaseDirectory, "stream.sdp");
            var sdp = channel.Sdps.Values.First();
            File.WriteAllText(sdpPath, sdp.SdpString, System.Text.Encoding.UTF8);
            Console.WriteLine();
            Console.WriteLine($"已生成 SDP 文件: {Path.GetFullPath(sdpPath)}");
            Console.WriteLine("接收测试请在本机或同网段另一台机器执行:");
            Console.WriteLine("  ffplay -i stream.sdp");
            Console.WriteLine("(若 ffplay 不在当前目录，请使用上面完整路径)");
            if (OperatingSystem.IsWindows())
            {
                Console.WriteLine("若出现 WASAPI 找不到音频端点，请改用 DirectSound 后再播:");
                Console.WriteLine("  PowerShell: $env:SDL_AUDIODRIVER=\"directsound\"; ffplay -i stream.sdp");
                Console.WriteLine("  CMD:        set SDL_AUDIODRIVER=directsound && ffplay -i stream.sdp");
            }
            Console.WriteLine();

            // 预缓冲：先写入一定时长再启动定时器，避免小 PTime 时发送端无包可发导致卡顿/慢放
            const int preBufferMs = 200;
            int preBufferBytes = (int)(waveFormat.AverageBytesPerSecond * (preBufferMs / 1000.0));
            int chunkSizeMax = Math.Max(preBufferBytes, 4096);
            var readBuffer = new byte[chunkSizeMax];
            int totalRead = 0;
            int preRead = 0;
            while (preRead < preBufferBytes)
            {
                int r = audioReader.Read(readBuffer, 0, Math.Min(readBuffer.Length, preBufferBytes - preRead));
                if (r <= 0) break;
                channel.Write(readBuffer, 0, r);
                preRead += r;
                totalRead += r;
            }
            Console.WriteLine($"预缓冲 {preRead} 字节（约 {preRead * 1000.0 / waveFormat.AverageBytesPerSecond:F0} ms），PTime={manager.PTimeμs} μs");

            // 读取间隔要短于 channel 消耗节奏，且每次读 2 倍间隔的数据量，保证持续领先
            const double readIntervalMs = 1.0;  // 每 1ms 触发一次，跟上小 PTime 的发送节奏
            const double readAheadMultiplier = 2.0;  // 每次读入 2 倍“间隔时长”的数据，避免跟不上
            int chunkSize = (int)(waveFormat.AverageBytesPerSecond * (readIntervalMs / 1000.0) * readAheadMultiplier);
            chunkSize = Math.Max(Math.Min(chunkSize, readBuffer.Length), 256);
            bool readingComplete = false;
            var startedUtc = DateTime.UtcNow;

            HighPrecisionTimer? readTimer = null;
            readTimer = new HighPrecisionTimer(() =>
            {
                if (Volatile.Read(ref readingComplete)) return;
                if (durationSeconds > 0 && (DateTime.UtcNow - startedUtc).TotalSeconds >= durationSeconds)
                {
                    Volatile.Write(ref readingComplete, true);
                    readTimer?.Stop();
                    return;
                }
                int toRead = Math.Min(chunkSize, readBuffer.Length);
                int read = audioReader.Read(readBuffer, 0, toRead);
                if (read <= 0)
                {
                    Volatile.Write(ref readingComplete, true);
                    readTimer?.Stop();
                    return;
                }
                channel.Write(readBuffer, 0, read);
                Interlocked.Add(ref totalRead, read);
            });
            readTimer.SetPeriod(readIntervalMs);
            readTimer.Start();
            Console.WriteLine($"定时器驱动读取：每 {readIntervalMs} ms 读约 {chunkSize} 字节（约 {readAheadMultiplier}x 实时）");

            while (!Volatile.Read(ref readingComplete))
                Thread.Sleep(50);

            readTimer.Dispose();

            // 按已发送音频时长等待，确保缓冲区以实时速率发完再停止
            int total = Volatile.Read(ref totalRead);
            double sentDurationSeconds = total / (double)waveFormat.AverageBytesPerSecond;
            int waitSeconds = Math.Max(10, (int)Math.Ceiling(sentDurationSeconds) + 10);
            Console.WriteLine($"已写入 PCM 字节数: {total}（约 {sentDurationSeconds:F1} 秒），等待 {waitSeconds} 秒供缓冲区发送完毕...");
            for (int remaining = waitSeconds; remaining > 0; remaining--)
            {
                Thread.Sleep(1000);
                if (remaining % 30 == 0 || remaining <= 5)
                    Console.WriteLine($"  剩余 {remaining} 秒");
            }

            manager.StopChannel(channel);
        }
        finally
        {
            Aes67ChannelManager.Stop();
        }

        Console.WriteLine("AES67 文件广播结束。");
    }

    /// <summary>
    /// 获取第一个可用的本机 IPv4 地址（非回环、网卡已连接）。
    /// </summary>
    public static IPAddress? GetFirstLocalIPv4()
    {
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up)
                continue;

            foreach (var addr in ni.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily == AddressFamily.InterNetwork
                    && !IPAddress.IsLoopback(addr.Address))
                {
                    return addr.Address;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 列出本机可用于 AES67 的 IPv4 地址。
    /// </summary>
    public static List<string> GetLocalIPv4List()
    {
        var list = new List<string>();
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up)
                continue;

            foreach (var addr in ni.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily == AddressFamily.InterNetwork
                    && !IPAddress.IsLoopback(addr.Address))
                {
                    list.Add(addr.Address.ToString());
                }
            }
        }

        return list;
    }
}
