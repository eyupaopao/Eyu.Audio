using Eyu.Audio.Utils;
using System;

namespace Eyu.Audio.PTP;

public class PTPMessage
{
    public byte[] RawData { get; private set; }
    public PTPMessage(byte[] message)
    {
        RawData = message;
        TransportSpecific = message[0] & 0xF0 >> 4;
        MessageId = message[0] & 0x0F;
        Version = message[1] & 0x0F;
        Length = message.ReadInt16BE(2); // 2,3 byte
        Domain = message[4];
        Flags = message.ReadInt16BE(6); // 6,7 byte
        //var bytes = message[8..16];
        CorrectionField = message[8..16];// offset 8; 8 bytes
        SourcePortIdentity = message[20..30]; //offset 20; 10 bytes
        SequencId = message.ReadInt16BE(30);
        Control = message[32];
        LogMeanMessageInterval = message[33];
        if (MessageId >= MessageType.SYNC && MessageId <= MessageType.PATH_DELAY_FOLLOW_UP)
        {
            var timestamp = ReadPtpTimestamp(message);
            Timestamp = new PTPTimestamp(timestamp[0], timestamp[1]);
        }
    }
    /// <summary>
    /// 从PTP消息中读取时间戳，返回包含秒和纳秒的数组
    /// long[0] = 秒数
    /// long[1] = 纳秒数 (0-999,999,999)
    /// </summary>
    /// <param name="message">PTP消息缓冲区</param>
    /// <returns>包含秒和纳秒的数组</returns>
    public long[] ReadPtpTimestamp(byte[] message)
    {
        long[] timestamp = new long[2];
        // 读取秒数部分（48位，PTP标准中通常为48位）
        // 前16位在偏移34，后32位在偏移36
        ushort secondsHigh = message.ReadUInt16BE(34);  // 高16位
        uint secondsLow = message.ReadUInt32BE(36);     // 低32位
        timestamp[0] = (long)secondsHigh << 32 | secondsLow;  // 合并为48位秒数

        // 读取纳秒部分（32位，偏移40）
        uint nanoseconds = message.ReadUInt32BE(40);
        timestamp[1] = nanoseconds;  // 转为long类型

        // 确保纳秒值在有效范围内（0-999,999,999）
        if (timestamp[1] > 999_999_999)
        {
            // 处理异常情况，可能是数据错误
            timestamp[0] += timestamp[1] / 1_000_000_000;
            timestamp[1] %= 1_000_000_000;
        }

        return timestamp;
    }
    /// <summary>
    /// offset 0; 7-4bit
    /// 传送相关
    /// 在使用 UDP/IP 协议封装时：
    /// <list type="table">
    /// <item>0 UDP/IPv6 封装时，接收者忽略这个域</item>
    /// <item>1 UDP/IPv4 封装时，这个域为 1，表示接收者需要将 PTP 事件消息的 UDP 负载填充到 124 字节</item>
    /// </list>
    /// 在使用以太网封装时：
    /// <list type="table">
    /// <item>0 表示PTP消息由1588协议使用</item>
    /// <item>1 表示PTP消息由802.1as协议使用</item>
    /// </list>
    /// </summary>
    public int TransportSpecific { get; set; }
    /// <summary>
    /// offset 0; 0-3bit
    /// 消息的类型
    /// <list type="bullet">
    /// <item>Event <list type="number">
    /// <item>SYNC:0x1</item> 
    /// <item>DELAY_REQ:0x2</item> 
    /// <item>PATH_DELAY_REQ:0x3</item> 
    /// <item>PATH_DELAY_RESP:0x4</item> 
    /// <item>Reserved:0x4– 0x7</item> 
    /// </list>
    /// </item>
    /// <item>General<list type="number">
    /// <item>FOLLOW_UP:0x8</item>
    /// <item>DELAY_RESP:0x9</item>
    /// <item>PATH_DELAY_FOLLOW_UP:0xA</item>
    /// <item>ANNOUNCE:0xB</item>
    /// <item>SIGNALING:0xC</item>
    /// <item>MANAGEMENT:0xD</item>
    /// <item>Reserved:0xE– 0xF</item>
    /// </list>
    /// </item>
    /// </list>
    /// </summary>
    public int MessageId { get; set; }
    /// <summary>
    /// offset 1; 0-3bit <br/>
    /// 消息版本
    /// </summary>
    public int Version { get; set; }
    public int Length { get; set; }
    public int Domain { get; set; }
    #region flags

    /// <summary>
    /// PTPv2 flags 字段包含关于消息类型的进一步详细信息，尤其在使用一步或两步实现的情况下。一步或两步的实现由 flags 字段的前八位中的 TWO_STEP 位控制。
    /// </summary>
    public int Flags { get; set; }
    public bool PTP_LI_61 => (Flags & FlagField.PTP_LI_61) == FlagField.PTP_LI_61;
    public bool PTP_LI_59 => (Flags & FlagField.PTP_LI_59) == FlagField.PTP_LI_61;
    public bool PTP_UTC_REASONABLE => (Flags & FlagField.PTP_UTC_REASONABLE) == FlagField.PTP_UTC_REASONABLE;
    public bool PTP_TIMESCALE => (Flags & FlagField.PTP_TIMESCALE) == FlagField.PTP_TIMESCALE;
    public bool TIME_TRACEABLE => (Flags & FlagField.TIME_TRACEABLE) == FlagField.TIME_TRACEABLE;
    public bool FREQUENCY_TRACEABLE => (Flags & FlagField.FREQUENCY_TRACEABLE) == FlagField.FREQUENCY_TRACEABLE;
    public bool PTP_ALTERNATE_MASTER => (Flags & FlagField.PTP_ALTERNATE_MASTER) == FlagField.PTP_ALTERNATE_MASTER;
    public bool PTP_TWO_STEP => (Flags & FlagField.PTP_TWO_STEP) == FlagField.PTP_TWO_STEP;
    public bool PTP_UNICAST => (Flags & FlagField.PTP_UNICAST) == FlagField.PTP_UNICAST;
    public bool PTP_Profile_Specific_1 => (Flags & FlagField.PTP_Profile_Specific_1) == FlagField.PTP_Profile_Specific_1;
    public bool PTP_Profile_Specific_2 => (Flags & FlagField.PTP_Profile_Specific_2) == FlagField.PTP_Profile_Specific_2;
    public bool PTP_SECURITY => (Flags & FlagField.PTP_SECURITY) == FlagField.PTP_SECURITY;
    #endregion
    /// <summary>
    /// 偏移 8, 8byte
    /// 修正域，各报文都有，主要用在 Sync 报文中， 用于补偿网中的传输时延，E2E 的频率同步
    /// </summary>
    public byte[] CorrectionField { get; set; }

    /// <summary>
    /// 源端口标识符，发送该消息时钟的 ID 和端口号
    /// offset 20, 10 bytes
    /// </summary>
    public byte[] SourcePortIdentity { get; set; }

    /// <summary>
    /// 序列号 ID，表示消息的序列号，以及关联消息的对应关系
    /// </summary>
    public int SequencId { get; set; }

    /// <summary>
    /// 控制域（IEEE 1588v1），由消息类型决定，定义：
    /// <list type="table">
    /// <item>0x00 Sync</item>
    /// <item>0x01 Delay_Req</item>
    /// <item>0x02 Follow_Up</item>
    /// <item>0x03 Delay_Resp</item>
    /// <item>0x04 Management</item>
    /// <item>0x05 All others</item>
    /// <item>0x06-0xff reserved</item>
    /// </list>
    /// </summary>
    public int Control { get; set; }

    /// <summary>
    /// 消息周期，PTP 消息的发送时间间隔。
    /// </summary>
    public int LogMeanMessageInterval { get; set; }


    /// <summary>
    /// 2位long数组第一位是秒，第二位是剩下的纳秒数。
    /// <list type="bullet">
    /// <item>Sync/Delay_Reg/Pdelay_Req: 源时间标签。</item>
    /// <item>    
    /// Follow_Up: 精确源时间标签
    /// <br/>PTP提供传输时间戳的机制，这个时间戳包括事件消息产生的时刻和相应的修正域，
    /// <br/>通过这个机制保证接收方接收到的是最精确的时间戳。
    /// <br/>在实际应用中时间戳分布在：originTimestamp或者preciseOriginstamp和correctionField中，由具体的执行决定。
    /// </item>
    /// <item>
    /// Delay_Resp: 接收时间戳。
    /// </item>
    /// <item>Pdelay_Resp: 请求接收时间戳。</item>
    /// <item>
    /// Pdelay_Resp_Follow_Up: 响应源时间戳
    /// </item>
    /// </list>
    /// </summary>
    public PTPTimestamp Timestamp { get; private set; }
    // For ANNOUNCE messages only
    public byte Priority1 { get; set; }
    public byte Priority2 { get; set; }
    public byte ClockClass { get; set; }
    public byte ClockAccuracy { get; set; }
    public ushort ClockVariance { get; set; }
    public byte StepsRemoved { get; set; }
    public byte TimeSource { get; set; }

    /// <summary>
    /// <list type="number">    
    /// <item>Sync/Delay_Reg: 0 byte</item>
    /// <item>Follow_Up: 0 byte</item>
    /// <item>Delay_Resp: 10 byte requestingPortIdentity 请求端口标识。</item>
    /// <item>Pdelay_Req: 10 byte Reserved 保留</item>
    /// <item>Pdelay_Resp: 10 byte requestingPortIdentity 请求端口标识</item>
    /// <item>Pdelay_Resp_Follow_Up: 10 byte requestingPortIdentity 请求端口标识</item>
    /// <item>Signaling: 10 byte targetPortIdentity 目的端口标识。<br/>targetPortIdentity的取值要求为本消息目的地址对应端口的portIdentity。</item>
    /// <item>managementTLV: m bytes 管理消息。</item>
    /// </list>
    /// </summary>
    public byte[] Payload { get; set; }

    public void ParseAnnounceFields(byte[] message)
    {
        if (MessageId == MessageType.ANNOUNCE)
        {
            Priority1 = message[44];
            ClockClass = message[45];
            ClockAccuracy = message[46];
            ClockVariance = (ushort)(message[47] << 8 | message[48]);
            Priority2 = message[49];
            TimeSource = message[51];
            StepsRemoved = message[52];
        }
    }


}
