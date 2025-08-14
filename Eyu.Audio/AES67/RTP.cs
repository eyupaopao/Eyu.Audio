using NAudio.Wave;
using System;
using System.Net;

namespace Eyu.Audio.AES67;
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
    private IWaveProvider _waveProvider;

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
    private uint _timestamp;

    // 上次同步时间(纳秒)
    private long _lastSyncNanoTime;

    // 时间同步间隔(包数)
    private const int SyncInterval = 100;

    // 已发送包计数，用于定期同步
    private int _packetCounter;

    PTPClient _pTPClient;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="payloadType">RTP负载类型</param>
    /// <param name="sampleRate">采样率(Hz)</param>
    /// <param name="channels">声道数</param>
    /// <param name="bitsPerSample">采样位数</param>
    /// <param name="packageTime">包间隔(ms)</param>
    public PcmToRtpConverter(IWaveProvider waveProvider, PTPClient pTPClient, byte payloadType, uint ssrc,int samplesPerPacket)
    {
        _payloadType = payloadType;
        _sampleRate = waveProvider.WaveFormat.SampleRate;
        _channels = waveProvider.WaveFormat.Channels;
        _bitsPerSample = waveProvider.WaveFormat.BitsPerSample;
        // 计算每个RTP包中的采样数（向上取整确保足够的数据）
        _samplesPerPacket = samplesPerPacket;
        Ssrc = ssrc;
        _waveProvider = waveProvider;
        _pTPClient = pTPClient;

        // 初始化序列号
        var random = new Random();
        _sequenceNumber = (ushort)random.Next(0, ushort.MaxValue);

        // 初始化时间戳为当前PTPClient时间对应的RTP时间戳
        InitializeTimestamp();
    }

    /// <summary>
    /// 初始化RTP时间戳
    /// </summary>
    private void InitializeTimestamp()
    {
        long currentNano = _pTPClient.TimeStampNanoseconds;
        _timestamp = NanoToRtpTimestamp(currentNano, _sampleRate);
        _lastSyncNanoTime = currentNano;
    }


    public byte[] ReadRtpFrame()
    {
        // 计算每个包需要的字节数：采样数 × 声道数 × 每个采样的字节数
        int bytesPerSample = _bitsPerSample / 8;
        int bytesNeeded = _samplesPerPacket * _channels * bytesPerSample;
        // 读取音频数据
        var buffer = new byte[bytesNeeded];
        _waveProvider.Read(buffer, 0, buffer.Length);

        // 构建并返回RTP包
        return BuildRtpPacket(buffer);
    }

    /// <summary>
    /// 构建RTP包
    /// </summary>
    /// <param name="payload">音频负载数据</param>
    /// <returns>完整的RTP包</returns>
    private byte[] BuildRtpPacket(byte[] payload)
    {
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
            _timestamp += (uint)_samplesPerPacket;
        }

        // RTP包头长度 (12字节) + 负载长度
        int packetSize = 12 + payload.Length;
        byte[] rtpPacket = new byte[packetSize];

        // 版本(2位) + 填充(1位) + 扩展(1位) + CSRC计数(4位)
        rtpPacket[0] = (byte)((RtpVersion << 6) | 0x00);

        // 标记位(1位) + 负载类型(7位)
        rtpPacket[1] = _payloadType;

        // 序列号 (2字节)
        byte[] sequenceBytes = BitConverter.GetBytes(NetworkByteOrder(_sequenceNumber));
        sequenceBytes.CopyTo(rtpPacket, 2);
        _sequenceNumber++; // 递增序列号

        // 时间戳 (4字节)
        byte[] timestampBytes = BitConverter.GetBytes(NetworkByteOrder(_timestamp));
        timestampBytes.CopyTo(rtpPacket, 4);

        // SSRC (4字节)
        byte[] ssrcBytes = BitConverter.GetBytes(NetworkByteOrder(Ssrc));
        ssrcBytes.CopyTo(rtpPacket, 8);

        // 负载数据
        Array.Copy(payload, 0, rtpPacket, 12, payload.Length);

        return rtpPacket;
    }

    /// <summary>
    /// 与PTPClient时间同步，校正可能的漂移
    /// </summary>
    private void SyncWithPTPClient()
    {
        long currentNano = _pTPClient.TimeStampNanoseconds;
        long timeElapsedNano = currentNano - _lastSyncNanoTime;

        // 计算理论上应该经过的采样数
        double expectedSamples = timeElapsedNano * (_sampleRate / 1e9d);
        uint expectedTimestamp = (uint)(_timestamp + expectedSamples);

        // 计算实际当前时间对应的RTP时间戳
        uint actualTimestamp = NanoToRtpTimestamp(currentNano, _sampleRate);

        // 计算时间差，如果超过阈值则进行校正
        uint timeDiff = (uint)Math.Abs(expectedTimestamp - actualTimestamp);

        // 如果差异超过一个包的采样数，则进行同步校正
        if (timeDiff > _samplesPerPacket)
        {
            _timestamp = actualTimestamp;
        }
        else
        {
            // 差异不大时，仅轻微调整，避免跳变
            _timestamp += (uint)_samplesPerPacket;
        }

        _lastSyncNanoTime = currentNano;
    }

    /// <summary>
    /// 纳秒时间戳转换为RTP时间戳
    /// RTP时间戳：从开始计数起经过了几个采样
    /// 例如：48k采样率的音频经过一秒时间的rtp时间戳是48000
    /// </summary>
    /// <param name="nanoTimestamp">纳秒时间戳（1e-9秒）</param>
    /// <param name="sampleRate">RTP时钟频率（Hz，根据媒体类型确定）</param>
    /// <returns>RTP时间戳（32位无符号整数）</returns>
    public static uint NanoToRtpTimestamp(long nanoTimestamp, int sampleRate)
    {
        // 转换公式：RTP时间戳 = 纳秒时间戳 × (时钟频率 / 1e9)
        double rtpTimestamp = nanoTimestamp * (sampleRate / 1e9d);
        // RTP时间戳为32位无符号整数，取模防止溢出
        return (uint)(rtpTimestamp % uint.MaxValue);
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
}
