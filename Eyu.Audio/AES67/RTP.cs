using Eyu.Audio.PTP;
using NAudio.Wave;
using System;
using System.Linq;
using System.Net;

namespace Eyu.Audio.Aes67;
/**
        header:
          |0 1 2 3 4 5 6 7|0 1 2 3 4 5 6 7|0 1 2 3 4 5 6 7|0 1 2 3 4 5 6 7|
          |v  |p|x|cc     |m|pt           |sequence number                |
          |timestamp                                                      |
          |ssrc                                                           |
          |csrc  32bit * 0 ~ 15                                           |


       Version（V）：2 bits，RTP 版本号，现在用的是 2。（第一个 RTP 草案用的 1）
       Padding（P）：1 bit，如果设置了该字段，报文的末尾会包含一个或多个填充字节，这些填充字节不是 payload 的内容。最后一个填充字节标识了总共需要忽略多少个填充字节（包括自己）。Padding 可能会被一些加密算法使用，因为有些加密算法需要定长的数据块。Padding 也可能被一些更下层的协议使用，用来一次发送多个 RTP 包。
       Extension（X）：1 bit，如果设置了该字段，那么头数据后跟着一个拓展数据，用于自定义操作。RTP协议本身没有定义任何扩展。
       CSRC count（CC）：4 bits，CSRC 列表的长度。
       Marker（M）：1 bit，Marker 会在预设中进行定义（预设和 RTP 的关系可以参考 rfc3551，我的理解是预设是对 RTP 的补充，以达到某一类实际使用场景的需要），在报文流中用它来划分每一帧的边界。预设中可能会定义附加的 marker，或者移除 Marker 来拓展 payload type 字段的长度。
       Payload type（PT）: 7bits，该字段定义 RTP payload 的格式和他在预设中的意义。上层应用可能会定义一个（静态的类型码 <->payload 格式）映射关系。也可以用 RTP 协议外的方式来动态地定义 payload 类型。在一个 RTP Session 中 payload 类型可能会改变，但是不应该用 payload 类型来区分不同的媒体流，正如之前所说，不同的媒体流应该通过不同 Session 分别传输。
       Sequence number：16 bits，每发送一个 RTP 包该序列号 + 1，RTP 包的接收者可以通过它来确定丢包情况并且利用它来重排包的顺序。这个字段的初始值应该是随机的，这会让 known-plaintext 更加困难。
       Timestamp：32 bits，时间戳反映了 RTP 数据包生成第一块数据时的时刻。这个时间戳必须恒定地线性增长，因为它会被用来同步数据包和计算网络抖动，此外这个时钟解决方案必须有足够的精度，像是一个视频帧只有一个时钟嘀嗒这样是肯定不够的。如果 RTP 包是周期性的生成的话，通常会使用采样时钟而不是系统时钟，例如音频传输中每个 RTP 报文包含 20ms 的音频数据，那么相邻的下一个 RTP 报文的时间戳就是增加 20ms 而不是获取系统时间。和序列号一样时间戳的初始值也应该是随机的，而且如果多个 RTP 包是一次性生成的，那它们就会有相同的时间戳。不同媒体流的时间戳可能以不同的步幅增长，它们通常都是独立的，具有随机的偏移。这些时间戳虽然足以重建单一媒体流的时序，但是直接比较多个媒体流的时间戳是没办法进行同步的。每一时间戳都会和参考时钟（wallclock）组成时间对，而且需要同步的不同流会共用同一个参考时钟，通过对比不同流的时间对，就能计算出不同流的时间戳偏移量。这个时间对并不是和每个 RTP 包一同发送，而是通过 RTCP 协议，以一个相对较低的频率进行共享。
       SSRC：32 bits，该字段用来确定数据的发送源。这个身份标识应该随机生成，并且要保证同一个 RTP Session 中没有重复的 SSRC。虽然 SSRC 冲突的概率很小，但是每个 RTP 客户端都应该时刻警惕，如果发现冲突就要去解决。
       CSRC list：0 ~ 15 items， 32 bits each，CSRC list 表示对该 payload 数据做出贡献的所有 SSRC。这个字段包含的 SSRC 数量由 CC 字段定义。如果有超过 15 个 SSRC，只有 15 个可以被记录。 
        */
/// <summary>
/// PCM音频转RTP流转换器
/// </summary>
/// <summary>
/// PCM音频转RTP流转换器
/// </summary>
public class PcmToRtpConverter
{
    // RTP版本 (RFC 3550规定为2)
    private const byte RtpVersion = 2;

    // 音频负载类型 (动态范围: 96-127)
    private readonly byte _payloadType = 96;

    // 采样率 (Hz)
    private readonly int _sampleRate;

    // 声道数
    private readonly int _channels = 2;

    // 采样位数 (通常16位)
    private readonly int _bitsPerSample;

    // RTP序列号
    private ushort _sequenceNumber;

    // 同步源标识符
    public readonly uint Ssrc;

    // 每个RTP包中的采样数
    private readonly int _samplesPerPacket;

    // RTP时间戳
    private ulong _rtpTimestamp;

    // 上次同步时间(纳秒)
    private PTPTimestamp _lastSyncTime;

    // 时间同步间隔(包数)
    private const int SyncInterval = 100;

    // 已发送包计数，用于定期同步
    private int _packetCounter;

    PTPClient _pTPClient;

    // 上次发送RTP包的时间
    private PTPTimestamp _lastPacketTime;

    // 计算出的包间隔时间(纳秒)
    private PTPTimestamp _packetInterval;


    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="payloadType">RTP负载类型</param>
    /// <param name="sampleRate">采样率(Hz)</param>
    /// <param name="channels">声道数</param>
    /// <param name="bitsPerSample">采样位数</param>
    /// <param name="packageTime">包间隔(ms)</param>
    public PcmToRtpConverter(PTPClient pTPClient, int sampleRate, int bitsPerSample, int channels, byte payloadType, int samplesPerPacket, uint ssrc = 0)
    {
        _payloadType = payloadType;
        _sampleRate = sampleRate;
        _channels = channels;
        _bitsPerSample = bitsPerSample;
        _samplesPerPacket = samplesPerPacket;
        Ssrc = ssrc;
        _pTPClient = pTPClient;
        // 计算包间隔时间(纳秒)
        _packetInterval = new PTPTimestamp((ulong)((double)samplesPerPacket / sampleRate * 1e9d * 0.98));
        Initialize();
    }

    /// <summary>
    /// 初始化
    /// </summary>
    public void Initialize()
    {
        // 初始化序列号
        var random = new Random();
        _sequenceNumber = (ushort)random.Next(0, ushort.MaxValue);
        // 初始化RTP时间戳
        var currentTime = _pTPClient.Timestamp;
        _rtpTimestamp = PTPTimestampToRtpTimestamp(currentTime, (uint)_sampleRate);
        _lastSyncTime = currentTime;
        // 初始化上次发送时间为当前时间减去一个间隔，确保第一个包可以立即发送
        _lastPacketTime = _pTPClient.Timestamp - _packetInterval;
    }

    public bool EnsureTime()
    {
        var currentTime = _pTPClient.Timestamp;
        // PTP 刚同步时时钟可能回退，导致 currentTime < _lastPacketTime，会一直发不出去。此时按“时钟回退”处理，重置上次发包时间。
        if (currentTime < _lastPacketTime)
        {
            _lastPacketTime = currentTime - _packetInterval;
            _lastSyncTime = currentTime;
        }
        if (currentTime < _lastPacketTime + _packetInterval)
        {
            // 时间未到，不生成包
            return false;
        }
        return true;
    }

    /// <summary>
    /// 构建RTP包（添加时间检查）
    /// </summary>
    /// <param name="payload">音频负载数据</param>
    /// <returns>完整的RTP包，若时间未到则返回null</returns>
    public byte[] BuildRtpPacket(byte[] payload, int offset, int count)
    {

        _packetInterval = new PTPTimestamp((ulong)((double)_samplesPerPacket / _sampleRate * 1e9d * 0.992));
        var currentTime = _pTPClient.Timestamp;
        if (payload.Length - offset < count)
        {
            count = payload.Length - offset;
        }

        // 定期与PTP时间同步，防止漂移
        _packetCounter++;
        if (_packetCounter >= SyncInterval)
        {
            SyncWithPTPClient();
            _packetCounter = 0;
        }
        else
        {
            // 正常递增时间戳（每个包包含_samplesPerPacket个采样）
            _rtpTimestamp += (ulong)_samplesPerPacket;
        }

        // 更新上次发送时间
        _lastPacketTime = currentTime;

        // RTP包头长度 (12字节) + 负载长度
        int packetSize = 12 + count;
        byte[] rtpPacket = new byte[packetSize];

        // 版本(2位) + 填充(1位) + 扩展(1位) + CSRC计数(4位)
        rtpPacket[0] = (RtpVersion << 6) | 0x00;

        // 标记位(1位) + 负载类型(7位)
        rtpPacket[1] = _payloadType;

        // 序列号 (2字节)
        byte[] sequenceBytes = BitConverter.GetBytes(NetworkByteOrder(_sequenceNumber));
        sequenceBytes.CopyTo(rtpPacket, 2);
        _sequenceNumber++; // 递增序列号

        // 时间戳 (4字节)
        // RTP时间戳为32位无符号整数，取模防止溢出
        byte[] timestampBytes = BitConverter.GetBytes(_rtpTimestamp);
        if (BitConverter.IsLittleEndian)
        {
            timestampBytes = timestampBytes.Reverse().ToArray();
        }
        Array.Copy(timestampBytes, 4, rtpPacket, 4, 4);

        // SSRC (4字节)
        byte[] ssrcBytes = BitConverter.GetBytes(NetworkByteOrder(Ssrc));
        ssrcBytes.CopyTo(rtpPacket, 8);
        Array.Copy(payload, offset, rtpPacket, 12, count);
        return rtpPacket;
    }

    /// <summary>
    /// 与PTPClient时间同步，校正可能的漂移
    /// </summary>
    private void SyncWithPTPClient()
    {
        var currentTime = _pTPClient.Timestamp;
        var timeElapsed = currentTime - _lastSyncTime;

        // 计算理论上应该经过的采样数
        var expectedSamples = timeElapsed.GetTotalNanoseconds() * (_sampleRate / 1e9d);
        var expectedTimestamp = _rtpTimestamp + (ulong)expectedSamples;

        // 计算实际当前时间对应的RTP时间戳
        var actualTimestamp = PTPTimestampToRtpTimestamp(currentTime, (uint)_sampleRate);

        // 计算时间差，如果超过阈值则进行校正
        var timeDiff = (int)(expectedTimestamp > actualTimestamp ? expectedTimestamp - actualTimestamp : actualTimestamp - expectedTimestamp);
        // 如果差异超过一个包的采样数，则进行同步校正
        if (timeDiff > _samplesPerPacket)
        {
            _rtpTimestamp = actualTimestamp;
        }
        else
        {
            // 差异不大时，仅轻微调整，避免跳变
            _rtpTimestamp += (ulong)_samplesPerPacket;
        }

        _lastSyncTime = currentTime;
        // 同步时也更新上次包时间，避免时间累积误差
        _lastPacketTime = currentTime;
    }

    static uint NsPerSecond = (uint)1e9;

    /// <summary>
    /// 纳秒时间戳转换为RTP时间戳
    /// RTP时间戳：从开始计数起经过了几个采样
    /// 例如：48k采样率的音频经过一秒时间的rtp时间戳是48000
    /// </summary>
    /// <param name="timestamp">时间戳</param>
    /// <param name="sampleRate">RTP时钟频率（Hz，根据媒体类型确定）</param>
    /// <returns>RTP时间戳</returns>
    public static ulong PTPTimestampToRtpTimestamp(PTPTimestamp timestamp, uint sampleRate)
    {
        ulong rtpTimestamp = (ulong)((timestamp.Seconds * sampleRate) + (timestamp.Nanoseconds * sampleRate / NsPerSecond));
        return rtpTimestamp;
    }

    public static ulong RtpTimestampToNano(ulong rtpTimestamp, uint sampleRate)
    {
        var seconds = rtpTimestamp / sampleRate;
        var remainingSsmples = rtpTimestamp % sampleRate;
        var nano = seconds * NsPerSecond + remainingSsmples * NsPerSecond / sampleRate;
        return nano;
    }

    /// <summary>
    /// 将16位无符号整数转换为网络字节序(大端序)
    /// </summary>
    private ushort NetworkByteOrder(ushort value)
    {
        if (BitConverter.IsLittleEndian)
            return (ushort)IPAddress.HostToNetworkOrder((short)value);
        return value;
    }

    /// <summary>
    /// 将32位无符号整数转换为网络字节序(大端序)
    /// </summary>
    private uint NetworkByteOrder(uint value)
    {
        if (BitConverter.IsLittleEndian)
            return (uint)IPAddress.HostToNetworkOrder((int)value);
        return value;
    }

    /// <summary>
    /// 将32位整数转换为网络字节序(大端序)
    /// </summary>
    private int NetworkByteOrder(int value)
    {
        if (BitConverter.IsLittleEndian)
            return IPAddress.HostToNetworkOrder(value);
        return value;
    }


}

