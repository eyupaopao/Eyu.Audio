using NullFX.CRC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace Eyu.Audio.AES67;


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

public class SdpFinder
{

}
public class Sdp
{
    public const string SdpMuticastAddress = "239.255.255.255";
    public const int SdpMuticastPort = 9875;
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
    public string Name { get; private set; }
    public string LocalAddress { get; private set; }
    public string MuticastAddress { get; private set; }
    public int MuticastPort { get; private set; }
    public uint SessId { get; private set; }
    public uint SessVersion { get; private set; }
    public float Ptime { get; private set; }
    public string PtpMaster { get; private set; }
    public int Domain { get; private set; }
    public string RtpMap { get; private set; }
    public string? Info { get; private set; }
    public byte[] Bytes { get; private set; }

    public string AudioEncoding { get; private set; }
    public int SampleRate { get; private set; }
    public int SamplingRate { get; private set; }
    public int Channels { get; private set; }

    private int FrameCount;

    public Sdp(string name,
               uint sessId,
               string localAddress,
               string muticastAddress,
               int muticastPort,
               string ptpMaster,
               int domain,
               float ptime,
               string encode,
               int sampleRate,
               int channels,
               string? info)
    {
        Name = name;
        LocalAddress = localAddress;
        MuticastAddress = muticastAddress;
        MuticastPort = muticastPort;
        SessId = sessId;
        SessVersion = sessId;
        Ptime = ptime;
        PtpMaster = ptpMaster;
        Domain = domain;
        AudioEncoding = encode;
        SampleRate = sampleRate;
        Channels = channels;
        FrameCount = (int)Math.Round(sampleRate / 1000 * ptime);
        RtpMap = $"96 {encode}/{sampleRate}/{channels}";
        //RtpMap = rtpMap;
        Info = info;
        BuildSdpBytes();
    }

    public Sdp(byte[] sdbBytes)
    {
        Bytes = sdbBytes;
        AnalysisSdp(); // 在构造函数中调用解析方法
    }

    private void BuildSdpBytes()
    {
        var sdpConfig = new List<string>{
            $"v=0",                                           // sdp版本号固定为0
            $"o=- {SessId} {SessVersion} IN IP4 {LocalAddress}",      // 会话源信息
            $"s={Name}",                                      // 会话名称
            $"c=IN IP4 {MuticastAddress}/32",                   // 连接信息
            $"t=0 0",                                         // 活动时间 开始时间和结束时间均为0，表示会话永久有效。
            $"m=audio {MuticastPort} RTP/AVP 96",                     // 媒体名称和传输地址
        };
        if (Info != null)
        {
            sdpConfig.Add($"i={Info}");
        }
        sdpConfig.AddRange(new[] {
            $"a=rtpmap:{RtpMap}",                              // rtp映射：负载类型96：编码格式L24，采样率48000,声道数1。
            $"a=recvonly",                                     // 媒体方向，接收方仅接收。
            $"a=ptime:{Ptime}",                                // 包时间ms
            $"a=mediaclk:direct=0",                            // 媒体时钟，使用直接时钟同步参数0表示默认配置
            $"a=ts-refclk:ptp=IEEE1588-2008:{PtpMaster}:{Domain}",  //时钟源:域
            $"a=framecount:{FrameCount}",                      // 包采样数
        });
        Bytes = Encoding.Default.GetBytes(string.Join("\r\n", sdpConfig));
    }

    private void AnalysisSdp()
    {
        if (Bytes == null || Bytes.Length == 0)
            throw new InvalidOperationException("无法解析空的SDP字节数组");

        // 将字节数组转换为字符串
        string sdpString = Encoding.Default.GetString(Bytes);
        // 按行分割SDP内容
        string[] lines = sdpString.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // 查找每行的类型标识符和值的分隔符
            int equalsIndex = line.IndexOf('=');
            if (equalsIndex <= 0)
                continue;

            string type = line.Substring(0, equalsIndex);
            string value = line.Substring(equalsIndex + 1);

            // 根据SDP行类型进行解析
            switch (type)
            {
                case "s":
                    // 解析会话名称
                    Name = value;
                    break;
                case "o":
                    // 解析会话源信息: o=- {SessId} {SessVersion} IN IP4 {LocalAddress}
                    string[] originParts = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (originParts.Length >= 6)
                    {
                        SessId = uint.Parse(originParts[1]);
                        SessVersion = uint.Parse(originParts[2]);
                        LocalAddress = originParts[5];
                    }
                    break;
                case "c":
                    // 解析连接信息: c=IN IP4 {MuticastAddress}/32
                    string[] connectionParts = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (connectionParts.Length >= 3)
                    {
                        string addressWithPrefix = connectionParts[2];
                        int slashIndex = addressWithPrefix.IndexOf('/');
                        MuticastAddress = slashIndex > 0
                            ? addressWithPrefix.Substring(0, slashIndex)
                            : addressWithPrefix;
                    }
                    break;
                case "m":
                    // 解析媒体信息: m=audio {MuticastPort} RTP/AVP 96
                    string[] mediaParts = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (mediaParts.Length >= 2 && int.TryParse(mediaParts[1], out int port))
                    {
                        MuticastPort = port;
                    }
                    break;
                case "i":
                    // 解析信息字段
                    Info = value;
                    break;
                case "a":
                    // 解析属性字段
                    if (value.StartsWith("rtpmap:"))
                    {
                        RtpMap = value.Substring("rtpmap:".Length);
                        var rtpInfo = RtpMap.Split(' ')[1].Split('/');
                        AudioEncoding = rtpInfo[0];
                        SampleRate = int.Parse(rtpInfo[1]);
                        Channels = int.Parse(rtpInfo[2]);
                    }
                    else if (value.StartsWith("ptime:"))
                    {
                        if (int.TryParse(value.Substring("ptime:".Length), out int ptime))
                        {
                            Ptime = ptime;
                        }
                    }
                    else if (value.StartsWith("ts-refclk:ptp=IEEE1588-2008:"))
                    {
                        string ptpInfo = value.Substring("ts-refclk:ptp=IEEE1588-2008:".Length);
                        string[] ptpParts = ptpInfo.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                        if (ptpParts.Length >= 2)
                        {
                            PtpMaster = ptpParts[0];
                            int.TryParse(ptpParts[1], out int domain);
                            Domain = domain;
                        }
                    }
                    else if (value.StartsWith("a=framecount:"))
                    {
                        if (int.TryParse(value.Substring("a=framecount:".Length), out int frameCount))
                        {
                            FrameCount = frameCount;
                        }
                    }
                    break;
                // 忽略不需要解析的字段
                case "v":
                case "t":
                default:
                    break;
            }
        }
    }
}
