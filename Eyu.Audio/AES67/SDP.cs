using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Eyu.Audio.AES67
{
    public class SDP
    {
        UdpClient udpClient;
        byte[] sdpMsg;
        public SDP()
        {
            udpClient = new UdpClient();
        }
        public byte[] ConstructSDPMsg(string addr, int framecount, int ptime, string multicastAddr, int samplerate, int channels, string encoding, string name, string sessID, string sessVersion, string ptpMaster, string? info = null)
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
                "v=0",                                          // sdp版本号
                "o=- " + sessID + " " + sessVersion + " IN IP4 " + addr,  // 会话源信息
                "s=" + name,                                      // 会话名称
                "c=IN IP4 " + multicastAddr + "/32",                // 连接信息
                "t=0 0",                                        // 活动时间
                "a=clock-domain:PTPv2 0",                       // 会话属性行
                "m=audio 5004 RTP/AVP 96",                      // 媒体名称和传输地址
                "a=rtpmap:96 " + encoding + "/" + samplerate + "/" + channels,
                "a=sync-time:0",
                "a=framecount:" + framecount,
                "a=ptime:" + ptime,
                "a=mediaclk:direct=0",
                "a=ts-refclk:ptp=IEEE1588-2008:" + ptpMaster,
                "a=recvonly",
                "i=" + (info == null ? "*" : info),
                ""
            };
            var sdpString = string.Join("\r\n", sdpConfig);
            var sdpBody = Encoding.Default.GetBytes(sdpString);
            var sdpMsg = sapHader.Concat(sapContentType).Concat(sdpBody).ToArray();
            return sdpMsg;
        }

        public void Start(CancellationToken token)
        {
            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    if (sdpMsg == null || udpClient == null)
                    {
                        await Task.Delay(1);
                        continue;
                    }
                    udpClient.Send(sdpMsg, "239.255.255.255", 9875);
                    await Task.Delay(30 * 1000);
                }
            });
        }

    }
}
