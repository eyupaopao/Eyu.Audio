using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace Eyu.Audio.PTP;

public static class PTPGenerator
{
    public const byte version = 2;
    public static byte[] DelayReq(byte domain, byte[] clockId, ushort req_seq, int LogMsgInterval)
    {
        var length = 52;
        var buffer = new byte[length];
        // 每次获取都生成一个新的id，小于 0x10000;
        req_seq = req_seq += 1;
        buffer[0] = MessageType.DELAY_REQ;// type
        buffer[1] = version;
        // messagelenght
        buffer[2] = (byte)(length >> 8);
        buffer[3] = (byte)(length & 0xff);
        buffer[4] = domain;
        // 5-6 标志位
        buffer[6] = (byte)(FlagField.PTP_TWO_STEP >> 8);
        buffer[7] = (byte)(FlagField.PTP_TWO_STEP & 0xFF);
        // 8-15 （CorrectionField）
        Array.Clear(buffer, 8, 8);
        // 16-19 保留字段
        Array.Clear(buffer, 16, 4);
        // 20-27 源时钟ID
        Array.Copy(clockId, 0, buffer, 20, 8);
        // 28-29 源端口ID
        // 序列号 
        buffer[30] = (byte)(req_seq >> 8);
        buffer[31] = (byte)(req_seq & 0xff);
        // 控制域
        buffer[32] = ControlField.Delay_Req;
        // logMsgInterval 录入消息周期，PTP消息的发送时间间隔，由消息类型决定。
        // logMsgInterval 字段的值是一个整数（通常用符号logInterval表示），消息的实际发送间隔（T）通过以下公式计算：T = 2^logInterval 秒
        // Announce 消息：用于主从时钟角色协商，默认logInterval=1（即 2 秒），常见范围为-3（125ms）到5（32 秒）。
        // Sync 消息：如前文所述，默认值因模式而异（如 IEEE 1588v2 默认logInterval = -3，对应 125ms）。
        // Delay_Req / Delay_Resp 消息：从时钟向主时钟请求延迟测量，默认logInterval通常与 Sync 消息一致（确保延迟测量频率匹配同
        buffer[33] = (byte)LogMsgInterval;
        //PTP Specified Message Field n byte 34 PTP消息体和消息扩展字节。
        return buffer;
    }
    public static byte[] DelayResp(byte domain, byte[] clockId, ushort req_seq, byte[] receiveTimestamp, byte[] requestId)
    {
        var length = 54;
        var buffer = new byte[length];
        buffer[0] = MessageType.DELAY_RESP; // type
        buffer[1] = version;
        // 写长度 messagelenght
        buffer[2] = (byte)(length >> 8);
        buffer[3] = (byte)(length & 0xff);
        buffer[4] = domain;
        // 标志位 - 保持与Sync消息一致
        buffer[6] = (byte)(FlagField.PTP_TWO_STEP >> 8);
        buffer[7] = (byte)(FlagField.PTP_TWO_STEP & 0xFF);
        // 8-15 （CorrectionField）
        Array.Clear(buffer, 8, 8);
        // 16-19 保留字段
        Array.Clear(buffer, 16, 4);
        // 20-27 源时钟ID
        Array.Copy(clockId, 0, buffer, 20, 8);
        // 28-29 源端口ID
        // 序列号 
        buffer[30] = (byte)(req_seq >> 8);
        buffer[31] = (byte)(req_seq & 0xff);
        // 控制域
        buffer[32] = ControlField.Delay_Resp; // Delay_Resp
        buffer[33] = 0;
        // 接收到req的时间戳
        Array.Copy(receiveTimestamp, 0, buffer, 34, 10);
        // req请求源id
        Array.Copy(requestId, 0, buffer, 44, 10);
        return buffer;
    }
    public static byte[] Sync(byte domain, byte[] clockId, ushort seqId, int LogMsgInterval)
    {
        var length = 44;
        var buffer = new byte[length];
        buffer[0] = MessageType.SYNC; // type
        buffer[1] = version;
        // messagelenght
        buffer[2] = (byte)(length >> 8);
        buffer[3] = (byte)(length & 0xff);
        buffer[4] = domain;
        // 标志位 - 设置为两步时钟
        buffer[6] = (byte)(FlagField.PTP_TWO_STEP >> 8);
        buffer[7] = (byte)(FlagField.PTP_TWO_STEP & 0xFF);
        // 8-15 （CorrectionField）
        Array.Clear(buffer, 8, 8);
        // 16-19 保留字段
        Array.Clear(buffer, 16, 4);
        // 20-27 源时钟ID
        Array.Copy(clockId, 0, buffer, 20, 8);
        // 28-29 源端口ID
        // 序列号 
        buffer[30] = (byte)(seqId >> 8);
        buffer[31] = (byte)(seqId & 0xff);
        // 控制域
        buffer[32] = ControlField.Sync; // Sync
        //LogMsgInterval 录入消息周期，PTP消息的发送时间间隔，由消息类型决定。
        buffer[33] = (byte)LogMsgInterval;
        //PTP Specified Message Field n byte  34        PTP消息体和消息扩展字节。
        return buffer;
    }

    public static byte[] FollowUp(byte domain, byte[] clockId, ushort seqId, int LogMsgInterval, byte[] syncTimestamp)
    {
        var length = 44;
        var buffer = new byte[length];
        buffer[0] = MessageType.FOLLOW_UP; // type
        buffer[1] = version;
        // 写长度 messagelenght
        buffer[2] = (byte)(length >> 8);
        buffer[3] = (byte)(length & 0xff);
        buffer[4] = domain;
        // 6-7 标志位 
        buffer[6] = (byte)(FlagField.PTP_TWO_STEP >> 8);
        buffer[7] = (byte)(FlagField.PTP_TWO_STEP & 0xFF);
        // 8-15 （CorrectionField）
        Array.Clear(buffer, 8, 8);
        // 16-19 保留字段
        Array.Clear(buffer, 16, 4);
        // 20-27 源时钟ID
        Array.Copy(clockId, 0, buffer, 20, 8);
        // 28-29 源端口ID
        // 序列号 
        buffer[30] = (byte)(seqId >> 8);
        buffer[31] = (byte)(seqId & 0xff);
        // 控制域
        buffer[32] = ControlField.Follow_Up; // Follow_Up
        buffer[33] = (byte)LogMsgInterval;
        // 发送sync的时间戳
        Array.Copy(syncTimestamp, 0, buffer, 34, 10);
        return buffer;
    }


    static ushort announce_seq = 0;
    /// <summary>
    /// 创建Announce消息
    /// </summary>
    public static byte[] Announce(byte Domain,
                                  byte[] serverIdBytes,
                                  int logMessageInterval,
                                  byte[] timestamp,
                                  int currentUTCOffset,
                                  byte priority1,
                                  byte clockClass,
                                  byte ClockAccuracy,
                                  int ClockVariance,
                                  byte priority2,
                                  byte timeSource,
                                  ushort stepsRemoved = 0)
    {
        var length = 64; // Announce消息长度
        var buffer = new byte[length];

        announce_seq = announce_seq += 1;

        // 消息类型
        buffer[0] = MessageType.ANNOUNCE; // type
        buffer[1] = version;
        // 消息长度
        buffer[2] = (byte)(length >> 8);
        buffer[3] = (byte)(length & 0xff);
        // 域号
        buffer[4] = (byte)Domain;
        // 6-7 标志位 
        // 8-15 （CorrectionField）
        Array.Clear(buffer, 8, 8);
        // 16-19 保留字段
        Array.Clear(buffer, 16, 4);
        // 源端口标识符 (前8字节是时钟ID，后2字节是端口号)
        // 20-27 源时钟ID
        Array.Copy(serverIdBytes, 0, buffer, 20, 8);
        // 端口号
        buffer[28] = 0; // 端口号高字节
        buffer[29] = 0; // 端口号低字节
        // 序列号
        buffer[30] = (byte)(announce_seq >> 8);
        buffer[31] = (byte)(announce_seq & 0xff);
        // 控制域
        buffer[32] = ControlField.AllOthers; // All others for announce
        // 消息间隔
        buffer[33] = (byte)logMessageInterval;
        // 当前时间戳
        //34  10  Origin Timestamp    数值为 0 或精度为 ±1 ns 的时间戳
        Array.Copy(timestamp, 0, buffer, 34, 10);
        //44  2   CurrentUtcOffset UTC 与 TAI 时间标尺间的闰秒时间差
        buffer[44] = (byte)(currentUTCOffset >> 8);
        buffer[45] = (byte)(currentUTCOffset & 0xff);
        //46  1   Reserved -
        buffer[46] = 0;
        //47  1   GrandmasterPriority1 用户定义的 grandmaster 优先级
        buffer[47] = priority1;
        //48  4   GrandmasterClockQuality grandmaster 的时间质量级别- 包含时钟等级(1字节)、时钟精度(1字节)、时钟方差(2字节)
        // 时钟等级
        buffer[48] = clockClass;
        // 时钟精度
        buffer[49] = ClockAccuracy;
        // 时钟方差
        buffer[50] = (byte)(ClockVariance >> 8);
        buffer[51] = (byte)(ClockVariance & 0xFF);
        //52  1   GrandmasterPriority2
        buffer[52] = priority2;
        //53  8   GrandmasterIdentity grandmaster 的时钟设备 ID
        Array.Copy(serverIdBytes, 0, buffer, 53, 8);
        //61 2   StepRemoved grandmaster 与 Slave 设备间的时钟路径跳数
        buffer[61] = (byte)(stepsRemoved >> 8); // 高字节
        buffer[62] = (byte)(stepsRemoved & 0xFF); // 低字节
        //63 1   TimeSource 时间源头类型：GPS - GPS 卫星传送时钟；PTP - PTP 时钟；NTF - NTP 时钟
        buffer[63] = timeSource;
        return buffer;
    }


    /*
字段	                长度	         offset     含义
TranSpec	        4bit	     0           传送相关: 0–表示PTP消息由1588协议使用 1–表示PTP消息由802.1as协议使用
MsgType 	        4bit	     0            表示消息类型。
                                            1588v2消息分为两类：事件消息（EVENT Message）和通用消息（General       Message）。
                                            事件报文是时间概念报文，进出设备端口时需要打上精确的时间戳，
                                            而通用报文则是非时间概念报文，进出设备不会产生时戳。
                                            类型值0 ~ 3的为事件消息，8 ~ D为通用消息。
                                            0x00: Sync
                                            0x01: Delay_Req
                                            0x02: Pdelay_Req
                                            0x03:Pdelay_Resp
                                            0x04-7: Reserved
                                            0x08: Follow_Up
                                            0x09: Delay_Resp
                                            0x0A: Pdelay_Resp_Follow_Up
                                            0x0B: Announce
                                            0x0C: Signaling
                                            0x0D: Management
                                            0x0E-0x0F: Reserved 
Reserved1	        4bit	     1          保留字段
VerPTP	            4bit	     1          表示1588协议的版本
MsgLength	        2byte	     2          PTP消息的长度，即PTP消息的全部字节数目。
                                            计入字节始于报头的第一个字节，同时包含并收尾于任何尾标的最后一个字节，或是无  尾标    成员  时收尾   于消息的最后一个字节。
DomainNumber	    1byte	     4           域编号，表示发送该消息时钟所属的域。
Reserved2	        1byte	     5           保留字段
FlagField	        2byte	     6           标志域（详情在后表）
CorrectionField	    8byte	     8           修正域，各报文都有，主要用在Sync报文中，用于补偿网络中的传输时延，E2E的频率同步。
Reserved3	        4byte	     16          保留字段
SourcePortIdentity	8byte	     20          源时钟ID
SourcePortIdentity	2byte	     28          源端口ID
SequenceID	        2byte	     30          序列号ID，表示消息的序列号，以及关联消息的对应关系。
ControlField	    1byte	     32          控制域，由消息类型决定：
                                            0x00：Sync
                                            0x01：Delay_Req
                                            0x02：Follow_Up
                                            0x03：Delay_Resp
                                            0x04：Management
                                            0x05：All others
                                            0x06-0xFF：reserved
LogMsgInterval 	    1byte	     33           录入消息周期，PTP消息的发送时间间隔，由消息类型决定。
PTP Specified Message Field	n byte	34        PTP消息体和消息扩展字节。


0	34	PTP Header	PTP 报文头

     */

    /// <summary>
    /// 生成服务器ID
    /// </summary>
    public static byte[] GenerateClockId()
    {
        // 使用网络接口的MAC地址生成唯一ID
        string clockId;
        try
        {
            var macAddress = GetMacAddress();
            clockId = BitConverter.ToString(macAddress).Replace("-", "").ToLower();
        }
        catch
        {
            // 如果获取MAC地址失败，使用随机ID
            clockId = Guid.NewGuid().ToString("N").Substring(0, 16).ToLower();
        }
        return GetClockIdBytes(clockId);
    }
    /// <summary>
    /// 将字符串转换为字节数组
    /// </summary>
    private static byte[] StringToByteArray(string hex)
    {
        return Enumerable.Range(0, hex.Length)
                         .Where(x => x % 2 == 0)
                         .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                         .ToArray();
    }

    /// <summary>
    /// 获取服务器ID字节数组
    /// </summary>
    private static byte[] GetClockIdBytes(string clockId)
    {
        var idBytes = new byte[8];
        var serverIdBytes = StringToByteArray(clockId.PadRight(16, '0').Substring(0, 16));
        Array.Copy(serverIdBytes, idBytes, 8);
        return idBytes;
    }

    /// <summary>
    /// 获取本机MAC地址
    /// </summary>
    public static byte[] GetMacAddress()
    {
        var networkInterfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
        foreach (var ni in networkInterfaces.Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up && n.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback))
        {
            var mac = ni.GetPhysicalAddress().GetAddressBytes();
            if (mac.Length == 6 && mac.Any(b => b != 0))
                return mac;
        }

        // 如果没有找到合适的网络接口，返回默认值
        return [0x00, 0x11, 0x22, 0x33, 0x44, 0x55];
    }

}
