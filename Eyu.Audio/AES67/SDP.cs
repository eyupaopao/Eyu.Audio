using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Eyu.Audio.AES67;

public static class SDP
{
    public static byte[]? SdpMessage;
    /// <summary>
    /// 会话公告协议（SAP，Session Announcement Protocol）的消息结构主要包括以下部分：
    /// 1. 版本号（Version）：一个4位的字段，用于指示SAP协议的版本。当前版本是1。
    /// 2. 地址类型（Address Type）：一个1位的字段，用于指示源地址是IPv4地址（0）还是IPv6地址（1）。
    /// 3. 消息类型（Message Type）：一个1位的字段，用于指示是会话公告（0）还是会话删除（1）。
    /// 4. 保留字段（Reserved）：一个1位的字段，保留未用。
    /// 5. 消息标识符哈希长度（MsgIdHash Length）：一个8位的字段，用于指示消息标识符哈希的长度。
    /// 6. 消息长度（Message Length）：一个16位的字段，用于指示消息体的长度。
    /// 7. 源地址（Originating Source）：一个32位（IPv4）或128位（IPv6）的字段，用于指示消息的源地址。
    /// 8. 消息体（Payload）：一个可变长度的字段，用于携带会话描述协议（SDP）的消息。
    /// 注意：这只是SAP协议的基本结构。实际的SAP消息可能会包含其他的字段，例如消息认证字段和消息标识符哈希字段。

    /// https://baike.sogou.com/v99608934.htm?fromTitle=SAP
    /// </summary>
    /// <param name="addr">本机地址</param>
    /// <param name="framecount"></param>
    /// <param name="ptime">包时间</param>
    /// <param name="multicastAddr">组播地址</param>
    /// <param name="samplerate">采样率</param>
    /// <param name="channels">通道数</param>
    /// <param name="encoding">编码格式</param>
    /// <param name="name">会话名称</param>
    /// <param name="sessID">会话源序号</param>
    /// <param name="sessVersion">会话源版本</param>
    /// <param name="ptpMaster">主时钟Id</param>
    /// <param name="info">其他信息</param>
    public static void ConstructSDPMsg(string addr, int port, int framecount, int ptime, string multicastAddr, int samplerate, int channels, string encoding, string name, string sessID, string sessVersion, string ptpMaster, string? info = null)
    {
        var random = new Random();
        var hash = (int)Math.Floor(random.NextDouble() * 65536);
        var sapHader = new byte[8];
        var sapContentType = Encoding.Default.GetBytes("application/sdp\0");
        var ip = addr.Split('.');
        sapHader[0] = 0x20;
        //sapHader.WriteInt16LE(hash, 2);
        sapHader[2] = (byte)(hash & 0xff);
        sapHader[3] = (byte)(hash >> 8);
        sapHader[4] = byte.Parse(ip[0]);
        sapHader[5] = byte.Parse(ip[1]);
        sapHader[6] = byte.Parse(ip[2]);
        sapHader[7] = byte.Parse(ip[3]);
        var sdpConfig = new List<string>{
            $"v=0",                                           // sdp版本号
            $"o=- {sessID} {sessVersion} IN IP4 {addr}",      // 会话源信息
            $"s={name}",                                      // 会话名称
            $"c=IN IP4 {multicastAddr}/32",                   // 连接信息
            $"t=0 0",                                         // 活动时间
            $"m=audio {port} RTP/AVP 96",                     // 媒体名称和传输地址
            $"i={(info == null ? "*" : info)}",
            $"a=clock-domain:PTPv2 0",                        // 会话属性行
            $"a=rtpmap:96 {encoding}/{samplerate} {channels}",
            $"a=sync-time:0",
            $"a=framecount:{framecount}",                     // 包采样数
            $"a=ptime:{ptime}",                               // 包时间
            $"a=mediaclk:direct=0",
            $"a=ts-refclk:ptp=IEEE1588-2008:{ptpMaster}",      //时钟源
            $"a=recvonly",
            $""
        };
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
        var sdpString = string.Join("\r\n", sdpConfig);
        var sdpBody = Encoding.Default.GetBytes(sdpString);
        SdpMessage = sapHader.Concat(sapContentType).Concat(sdpBody).ToArray();

    }



}
