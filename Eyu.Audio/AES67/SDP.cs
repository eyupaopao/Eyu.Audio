using NullFX.CRC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Eyu.Audio.Aes67;


/*
会话公告协议（SAP，Session Announcement Protocol）的消息结构主要包括以下部分：
1. 版本号（Version）：一个4位的字段，用于指示SAP协议的版本。当前版本是1。
2. 地址类型（Address Type）：一个1位的字段，用于指示源地址是IPv4地址（0）还是IPv6地址（1）。
3. 消息类型（Message Type）：一个1位的字段，用于指示是会话公告（0）还是会话删除（1）。
4. 保留字段（Reserved）：一个1位的字段，保留未用。
5. 消息标识符哈希长度（MsgIdHash Length）：一个8位的字段，用于指示消息标识符哈希的长度。
6. 消息长度（Message Length）：一个16位的字段，用于指示消息体的长度。
7. 源地址（Originating Source）：一个32位（IPv4）或128位（IPv6）的字段，用于指示消息的源地址。
8. 消息体（Payload）：一个可变长度的字段，用于携带会话描述协议（SDP）的消息。
注意：这只是SAP协议的基本结构。实际的SAP消息可能会包含其他的字段，例如消息认证字段和消息标识符哈希字段。
SAP协议头：
 0                   1                   2                   3
 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
| V=1 |A|R|T|E|C|   auth len    |         msg id hash           |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                                                               |
:                originating source (32 or 128 bits)            :
:                                                               :
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                    optional authentication data               |
:                              ....                             :
*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
|                      optional payload type                    |
+                                         +-+- - - - - - - - - -+
|                                         |0|                   |
+ - - - - - - - - - - - - - - - - - - - - +-+                   |
|                                                               |
:                            payload                            :
|                                                               |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
 */
    /// 1. v=：协议版本，目前为“0”。
    /// 2. o=：所有者/创建者和会话标识符。通常包括用户名、会话ID、会话版本、网络类型、地址类型和地址信息。
    /// 3. s=：会话名称。
    /// 4. i=：会话信息（可选）。
    /// 5. u=：URI of description（可选）。
    /// 6. e=：电子邮件地址（可选）。
    /// 7. p=：电话号码（可选）。
    /// 8. c=：连接信息（可选）-网络类型、地址类型和连接地址。
    /// 9. b=：带宽信息（可选）。
    /// 10. t=：时间描述，包括会话活动的起始和结束时间。
    /// 11. r=：零或多个重复时间（可选）。
    /// 12. z=：时区偏移（可选）。
    /// 13. k=：加密密钥（可选）。
    /// 14. a=：零或多个会话属性行（可选）。
    /// 15. m=：媒体名称和传输地址。
    /// 16. i=*：媒体标题（可选）。
    /// 17. c=*：连接信息（可选）。
    /// 18. b=*：带宽信息（可选）。
    /// 19. k=*：加密密钥（可选）。
    /// 20. a=*：零或多个媒体属性行（可选）。

public class Sdp
{
    #region sdp

    /// <summary>
    /// sdp id
    /// </summary>
    public string? Key { get; set; } = null!;
    /// <summary>
    /// 设备id
    /// </summary>
    public string? DevId { get; set; } = null!;
    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; set; } = null!;
    /// <summary>
    /// 发送源地址
    /// </summary>
    public string SourceIPAddress { get; set; } = null!;
    /// <summary>
    /// 组播地址
    /// </summary>
    public string MuticastAddress { get; set; } = null!;
    /// <summary>
    /// 组播端口
    /// </summary>
    public int MuticastPort { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public uint SessId { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public uint SessVersion { get; set; }
    /// <summary>
    /// 包时间
    /// </summary>
    public float PTimems { get; set; }
    /// <summary>
    /// 负载类型
    /// </summary>
    public int PayloadType { get; set; }
    /// <summary>
    /// 主时钟编码
    /// </summary>
    public string PtpMaster { get; set; } = null!;
    /// <summary>
    /// 时钟域
    /// </summary>
    public int Domain { get; set; }
    /// <summary>
    /// rtp映射
    /// </summary>
    public string RtpMap { get; set; } = null!;
    /// <summary>
    /// 会话信息
    /// </summary>
    public string? Info { get; set; } = null!;
    /// <summary>
    /// sap协议头
    /// </summary>
    public byte[] SapBytes { get; set; } = null!;
    /// <summary>
    /// 音频编码格式
    /// </summary>
    public string AudioEncoding { get; set; } = null!;
    /// <summary>
    /// 采样率
    /// </summary>
    public int SampleRate { get; set; }
    public int SamplingRate { get; set; }
    /// <summary>
    /// 通道数
    /// </summary>
    public int Channels { get; set; }
    /// <summary>
    /// 持续时间
    /// </summary>
    public int Duration { get; set; }
    /// <summary>
    /// 开始时间
    /// </summary>
    public long StartTime { get; set; } = 0;
    /// <summary>
    /// 结束时间
    /// </summary>
    public long StopTime { get; set; } = 0;
    /// <summary>
    /// 每包数据采样数
    /// </summary>

    private int SamplesPerFrame;
    /// <summary>
    /// 最后一次收到这个sdp包的时间。
    /// </summary>
    public DateTime LastReciveTime { get; set; }
    /// <summary>
    /// sdp原始字符串
    /// </summary>
    public string SdpString { get; set; } = null!;
    /// <summary>
    /// sdp原始字节码
    /// </summary>
    public byte[] SdpBytes { get; set; } = null!;

    #endregion
    #region sap
    /// <summary>
    /// sap包头
    /// </summary>
    public byte SapFlags { get; set; } = 0b0010_0000;
    /// <summary>
    /// sap版本
    /// </summary>
    public int SapVersion => (0b0010_0000 & SapFlags) >> 5;
    /// <summary>
    /// 地址类型
    /// </summary>
    public AddressFamily AddressType => (SapFlags & 0b0001_0000) == 0b0001_0000 ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork;
    /// <summary>
    /// "Deletion"：表示这个流被删除。 "Announcement"：表示流正常发送
    /// </summary>
    public string SapMessage => (SapFlags & 0b0000_0100) == 0b0000_0100 ? "Deletion" : "Announcement";
    /// <summary>
    /// sdp消息哈希
    /// </summary>
    public ushort MessageHash { get; set; }
    /// <summary>
    /// 源地址
    /// </summary>
    private byte[] SrcIp = null!;
    /// <summary>
    /// 认证长度（0 = 无认证，>0 = 存在认证数据）
    /// </summary>
    public int AuthLen { get; set; } = 0;
    /// <summary>
    /// 认证数据（如 HMAC 哈希，长度 = AuthLen）
    /// </summary>
    public byte[] AuthData { get; set; } = [];
    /// <summary>
    /// sap负载类型：固定"application/sdp"
    /// </summary>
    public string SapPayloadType { get; set; } = "application/sdp";
    public Sdp()
    {

    }
    #endregion
    public Sdp(string name,
               uint sessId,
               string localAddress,
               string muticastAddress,
               int muticastPort,
               string ptpMaster,
               int domain,
               float pTimems,
               int samplesPerPacket,
               string encoding,
               int sampleRate,
               int channels,
               int duration,
               string? info)
    {
        Name = name;
        SourceIPAddress = localAddress;
        MuticastAddress = muticastAddress;
        MuticastPort = muticastPort;
        SessId = sessId;
        SessVersion = sessId;
        PTimems = pTimems;
        PayloadType = Aes67Const.DefaultPayloadType;
        PtpMaster = ptpMaster;
        Domain = domain;
        AudioEncoding = encoding;
        SampleRate = sampleRate;
        Channels = channels;
        Duration = duration;
        if (Duration == 0)
        {
            StartTime = 0;
            StopTime = 0;
        }
        else
        {
            StartTime = PTPClient.Instance.Timestamp.Seconds;
            StopTime = StartTime + Duration;
        }
        SamplesPerFrame = samplesPerPacket;
        RtpMap = $"{PayloadType} {encoding}/{sampleRate}/{channels}";
        //RtpMap = rtpMap;
        Info = info;
        SrcIp = IPAddress.Parse(SourceIPAddress).GetAddressBytes();
        SapFlags = 0b0010_0000;
        if (IPAddress.TryParse(muticastAddress, out var address) && address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            SapFlags |= 0b0001_0000;
        }
        BuildSdpBytes();
        Key = $"{Name}{SessId}";
    }

    private void BuildSdpBytes()
    {
        var sdpConfig = new List<string>{
            $"v=0",                                                 // sdp版本号固定为0
            $"o=- {SessId} {SessVersion} IN IP4 {SourceIPAddress}", // 会话源信息
            $"s={Name}",                                            // 会话名称
            $"c=IN IP4 {MuticastAddress}/32",                       // 连接信息
            $"t={StartTime} {StopTime}",                            // 活动时间 开始时间和结束时间均为0，表示会话永久有效。
            $"m=audio {MuticastPort} RTP/AVP 96",                   // 媒体名称和传输地址
            $"a=charset:UTF-8",                                     // 关键：声明字符编码为UTF-8
        };
        if (Info != null)
        {
            sdpConfig.Add($"i={Info}");
        }
        sdpConfig.AddRange(new[] {
            $"a=rtpmap:{RtpMap}",                                   // rtp映射：负载类型96：编码格式L24，采样率48000,声道数1。
            $"a=recvonly",                                          // 媒体方向，接收方仅接收。
            $"a=ptime:{PTimems}",                                   // 包时间ms
            $"a=mediaclk:direct=0",                                 // 媒体时钟，使用直接时钟同步参数0表示默认配置
            $"a=ts-refclk:ptp=IEEE1588-2008:{PtpMaster}:{Domain}",  //时钟源:域
            $"a=framecount:{SamplesPerFrame}",                      // 包采样数
        });
        SdpString = string.Join("\r\n", sdpConfig);
        SdpBytes = Encoding.UTF8.GetBytes(SdpString);
        MessageHash = Crc16.ComputeChecksum(Crc16Algorithm.Standard, SdpBytes);
    }

    public byte[] BuildSap(bool deletion = false)
    {
        var sapHeader = new byte[8];
        sapHeader[0] = (byte)(deletion ? 0b0010_0100 : 0b0010_0000);
        sapHeader[2] = (byte)(MessageHash & 0xFF);
        sapHeader[3] = (byte)(MessageHash >> 8);
        Buffer.BlockCopy(SrcIp, 0, sapHeader, 4, 4);
        var sapContentType = Encoding.UTF8.GetBytes("application/sdp\0");
        SapBytes = sapHeader.Concat(sapContentType).Concat(SdpBytes).ToArray();
        return SapBytes;
    }



    public Sdp(byte[] sapBytes)
    {
        SapBytes = sapBytes;
        AnalysisSap();
        LastReciveTime = DateTime.Now;
    }

    #region 解析

    private void AnalysisSap()
    {
        if (SapBytes == null || SapBytes.Length < 8)
        {
            throw new ArgumentException("Invalid SAP bytes: insufficient length");
        }

        try
        {
            // 1. 解析SAP头部 (8字节)
            ParseSapHeader();

            // 2. 解析内容类型并定位SDP起始位置
            int sdpStartIndex = ParseContentType();

            // 3. 提取并解析SDP内容
            ParseSdpContent(sdpStartIndex);
            Key = $"{Name}{SessId}";
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to parse SAP packet", ex);
        }
    }

    private void ParseSapHeader()
    {
        SapFlags = SapBytes[0];
        AuthLen = SapBytes[1];
        MessageHash = (ushort)((SapBytes[3] << 8) | SapBytes[2]);
        // 提取源IP地址 (SAP头部4-7字节)
        byte[] srcIpBytes = new byte[4];
        Buffer.BlockCopy(SapBytes, 4, srcIpBytes, 0, 4);
        SourceIPAddress = new IPAddress(srcIpBytes).ToString();
        if (AuthLen > 0)
        {
            AuthData = new byte[AuthLen];
            Buffer.BlockCopy(SapBytes, 8, AuthData, 0, AuthLen);
        }
    }

    private int ParseContentType()
    {
        int contentTypeStart = 8 + AuthLen;

        // 查找内容类型的null终止符
        int nullTerminatorIndex = Array.IndexOf(SapBytes, (byte)0, contentTypeStart);
        if (nullTerminatorIndex == -1)
        {
            throw new FormatException("Content type null terminator not found");
        }

        // 提取并验证内容类型
        int contentTypeLength = nullTerminatorIndex - contentTypeStart;
        byte[] contentTypeBytes = new byte[contentTypeLength];
        Buffer.BlockCopy(SapBytes, contentTypeStart, contentTypeBytes, 0, contentTypeLength);

        string contentType = Encoding.UTF8.GetString(contentTypeBytes);
        if (contentType != SapPayloadType)
        {
            throw new FormatException($"Unexpected content type: {contentType}. Expected: {SapPayloadType}");
        }

        // 返回SDP内容的起始索引
        return nullTerminatorIndex + 1;
    }

    private void ParseSdpContent(int sdpStartIndex)
    {
        // 提取SDP字节并转换为字符串
        int sdpLength = SapBytes.Length - sdpStartIndex;
        SdpBytes = new byte[sdpLength];
        Buffer.BlockCopy(SapBytes, sdpStartIndex, SdpBytes, 0, sdpLength);
        SdpString = Encoding.Default.GetString(SdpBytes);

        // 按行解析SDP内容
        string[] lines = SdpString.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string line in lines)
        {
            ParseSdpLine(line);
        }
    }

    private void ParseSdpLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;

        int colonIndex = line.IndexOf('=');
        if (colonIndex <= 0) return;

        string field = line.Substring(0, colonIndex);
        string value = line.Substring(colonIndex + 1).Trim();

        switch (field)
        {
            case "v":
                // 协议版本，通常为0，不处理
                break;
            case "o":
                ParseOriginField(value);
                break;
            case "s":
                Name = value;
                break;
            case "c":
                ParseConnectionField(value);
                break;
            case "t":
                ParseTimeField(value);
                break;
            case "m":
                ParseMediaField(value);
                break;
            case "i":
                Info = value;
                break;
            case "a":
                ParseAttributeField(value);
                break;
        }
    }

    private void ParseOriginField(string value)
    {
        // 格式: - {SessId} {SessVersion} IN IP4 {address}
        string[] parts = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3)
        {
            uint.TryParse(parts[1], out uint sessId);
            SessId = sessId;
            uint.TryParse(parts[2], out uint sessVersion);
            SessVersion = sessVersion;
        }
    }

    private void ParseConnectionField(string value)
    {
        // 格式: IN IP4 {MuticastAddress}/32
        string[] parts = value.Split(new[] { ' ', '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3)
        {
            MuticastAddress = parts[2];
        }
    }
    private void ParseTimeField(string value)
    {
        string[] parts = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            StartTime = long.Parse(parts[0]);
            StopTime = long.Parse(parts[1]);
        }
    }

    private void ParseMediaField(string value)
    {
        // 格式: audio {MuticastPort} RTP/AVP 96
        string[] parts = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            int.TryParse(parts[1], out int port);
            MuticastPort = port;
        }
    }

    private void ParseAttributeField(string value)
    {
        if (value.StartsWith("rtpmap:"))
        {
            ParseRtpMap(value.Substring("rtpmap:".Length));
        }
        else if (value.StartsWith("ptime:"))
        {
            float.TryParse(value.Substring("ptime:".Length), out float ptime);
            PTimems = ptime;
        }
        else if (value.StartsWith("ts-refclk:ptp=IEEE1588-2008:"))
        {
            ParsePtpClock(value.Substring("ts-refclk:ptp=IEEE1588-2008:".Length));
        }
        else if (value.StartsWith("framecount:"))
        {
            int.TryParse(value.Substring("framecount:".Length), out int samples);
            SamplesPerFrame = samples;
        }
        // 可根据需要解析其他属性
    }

    private void ParseRtpMap(string value)
    {
        // 格式: {PayloadType} {AudioEncoding}/{SampleRate}/{Channels}
        string[] parts = value.Split(new[] { ' ', '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 4)
        {
            RtpMap = value;
            int.TryParse(parts[0], out int payloadType);
            PayloadType = payloadType;
            AudioEncoding = parts[1];
            int.TryParse(parts[2], out int sampleRate);
            SampleRate = sampleRate;
            SamplingRate = sampleRate; // 同步两个采样率属性
            int.TryParse(parts[3], out int channels);
            Channels = channels;
        }
    }

    private void ParsePtpClock(string value)
    {
        // 格式: {PtpMaster}:{Domain}
        string[] parts = value.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            PtpMaster = parts[0];
            int.TryParse(parts[1], out int domain);
            Domain = domain;
        }
    }
    #endregion

    public override string ToString()
    {
        return SapMessage + " " + SapPayloadType + "\r\n" + SdpString;
    }

    internal void SetInfo(string info)
    {
        Info = info;
        BuildSdpBytes();
    }
}
