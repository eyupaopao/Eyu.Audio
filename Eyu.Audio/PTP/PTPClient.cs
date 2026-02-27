using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Eyu.Audio.PTP
{
    public class PTPClient
    {
        // ptp v2 组播地址
        static string[] ptpMulticastAddrs = ["224.0.1.129", "224.0.1.130", "224.0.1.131", "224.0.1.132"];
        UdpClient ptpClockEvent;
        UdpClient ptpClockGeneral;
        // ptp 设置项
        // 选择一个地址
        public byte Domain { get; private set; }

        IPAddress domainAddress => IPAddress.Parse(ptpMulticastAddrs[Domain]);
        // 服务器配置
        public byte[] ClockId { get; private set; }
        public byte Priority1 { get; set; } = 128;  // 优先级1，数值越小优先级越高
        public byte Priority2 { get; set; } = 128;  // 优先级2
        public byte ClockClass { get; set; } = 248; // 时钟等级
        public byte ClockAccuracy { get; set; } = 0xFE; // 时钟精度
        public ushort ClockVariance { get; set; } = 25536; // 时钟方差
        public byte TimeSource { get; set; } = 0xA0; // 时间源
        // 最近一次同步时间(ms)
        long lastSync = 0;
        public bool IsRunning;
        public bool IsMaster { get; private set; } = true;
        // announce消息间隔指数 时间间隔为2的指数次方秒 -2表示250ms，-1表示500ms，0表示1s，1表示2s，以此类推
        int announceLogInterval = 1;
        double announceIntervalMs => Math.Pow(2, announceLogInterval) * 1000;
        // sync消息间隔指数 时间间隔为2的指数次方秒 
        int syncLogInterval = -2;
        double syncIntervalMs => Math.Pow(2, syncLogInterval) * 1000;
        // 当前主机id
        string ptpMaster = "";
        bool sync = false;
        // 本机地址
        string addr = "127.0.0.1";


        public bool IsSynced => sync;
        public string PtpMaster => ptpMaster;
        // 参数
        // 参与计算的各个时间戳
        PTPTimestamp t1, t2, t3, t4;

        public PTPTimestamp Delay { get; private set; }

        // 本机时间与服务器时间偏移量
        public PTPTimestamp Offset { get; private set; } = new PTPTimestamp(0);
        // sync 报文 id
        int sync_seq = 0;
        // delay_req 报文 id
        ushort req_seq = 0;
        static PTPClient instance;
        public static PTPClient Instance => instance ??= new PTPClient();

        /// <summary>
        /// 开始同步时间
        /// </summary>
        /// <param Name="addr">使用的地址</param>
        /// <param Name="domain">选择PTP域</param>
        /// <param Name="callback">连接成功回调函数</param>
        /// <param Name="syncInterval">同步间隔</param>
        public void Start(string? addr = null, byte domain = 0)
        {
            if (!string.IsNullOrEmpty(addr))
            {
                this.addr = addr;
            }
            Domain = domain;
            ClockId = PTPGenerator.GenerateClockId();
            cts = new CancellationTokenSource();
            Task.Run(GeneralHandler);
            Task.Run(EventHandler);
        }


        public PTPTimestamp Timestamp
        {
            get
            {
                return new PTPTimestamp(PTPTimmer.TimeStampNanoseconds) - Offset;
            }
        }

        public void Stop()
        {
            cts?.Cancel();
            ptpClockEvent.Close();
            ptpClockGeneral.Close();
        }

        // 获取与ptp服务器对时后的时间戳
        PTPTimestamp getCorrectedTime()
        {
            return new PTPTimestamp(PTPTimmer.TimeStampNanoseconds) - Offset;
        }

        CancellationTokenSource cts;


        private void GeneralHandler()
        {
            ptpClockGeneral = new UdpClient(320);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ptpClockGeneral.JoinMulticastGroup(domainAddress);
            }
            else
            {
                ptpClockGeneral.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(domainAddress, IPAddress.Any));
            }
            var remote = new IPEndPoint(IPAddress.Any, 0);
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    var buffer = ptpClockGeneral.Receive(ref remote);
                    if (buffer.Length < 31) continue;
                    var message = new PTPMessage(buffer);
                    // 检查版本和连接地址
                    if (message.Version != 2 || message.Domain != Domain)
                        continue;
                    switch (message.MessageId)
                    {
                        case MessageType.FOLLOW_UP:
                            HandleFollowUpMessage(message);
                            break;
                        case MessageType.DELAY_RESP:
                            HandleDelayRespMessage(message);
                            break;
                    }

                    // delay_resp报文：
                    if (message.MessageId == MessageType.DELAY_RESP && req_seq == message.SequencId)
                    {
                        // 获取主时钟收到delay_req的时间。
                        t4 = message.Timestamp;
                        // 计算延迟。
                        Delay = (t4 - t3 + t2 - t1) / 2;
                        var offset = (t2 - t1 - t4 + t3) / 2;
                        Offset += offset;
                        //if (Debugger.IsAttached)
                        //    Console.WriteLine($"同步：offset {offset}ns; delay {delay}ns；结果：{Offset}");
                        lastSync = getCorrectedTime().GetTotalNanoseconds() / 1000_000;
                        if (!sync)
                        {
                            sync = true;
                        }
                    }
                }
                catch (Exception ex)
                {

                }
            }
        }
        private void HandleDelayRespMessage(PTPMessage message)
        {
            // Only slave processes Delay_Resp messages
            if (IsMaster) return;
            if (message.SequencId == req_seq)
            {
                // 获取主时钟收到delay_req的时间。
                t4 = message.Timestamp;
                // 计算延迟。
                Delay = (t4 - t3 + t2 - t1) / 2;
                var offset = (t2 - t1 - t4 + t3) / 2;
                Offset += offset;
                if (!sync)
                {
                    sync = true;
                }
            }
        }
        /// <summary>
        /// 处理Follow-Up消息 - 从时钟获取Sync消息的精确发送时间
        /// </summary>
        private void HandleFollowUpMessage(PTPMessage message)
        {
            // Only slave processes Follow-Up messages
            if (IsMaster) return;

            // Ensure Follow-Up message matches the most recent Sync message (by sequence number)
            if (message.SequencId == sync_seq)
            {
                // sync 报文的精确发送时间 t1
                t1 = message.Timestamp;

                // Check if it's time to send a delay request to calculate network delay
                var currentTime = getCorrectedTime();
                if (currentTime.GetTotalNanoseconds() / 1_000_000 - lastSync > syncIntervalMs)
                {
                    // 发送delay_req报文
                    var delay_req = PTPGenerator.DelayReq(Domain, ClockId, req_seq++, 0);
                    ptpClockEvent.Send(delay_req, new IPEndPoint(IPAddress.Parse(ptpMulticastAddrs[Domain]), 319));
                    // 记录delay_req报文发送的时间。
                    t3 = getCorrectedTime();
                    lastSync = currentTime.GetTotalNanoseconds() / 1000_000;
                }
            }
        }

        private async void EventHandler()
        {

            ptpClockEvent = new UdpClient(319);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ptpClockEvent.JoinMulticastGroup(domainAddress);
            }
            else
            {
                ptpClockEvent.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(domainAddress, IPAddress.Any));
            }
            var remote = new IPEndPoint(IPAddress.Any, 0);
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    var buffer = ptpClockEvent.Receive(ref remote);
                    if (buffer.Length < 31) continue;
                    var message = new PTPMessage(buffer, getCorrectedTime());
                    // 检查版本和广播域
                    if (message.Version != 2 || message.Domain != Domain)
                        continue;
                    //只处理 sync 消息
                    if (message.MessageId != MessageType.SYNC)
                        continue;
                    // 是不是新的master时钟
                    if (message.SourcePortIdentityString != ptpMaster)
                    {
                        // 新时钟
                        ptpMaster = message.SourcePortIdentityString;
                        // 从新同步
                        sync = false;
                    }
                    //save sequence number
                    sync_seq = message.SequencId;
                    //check if master is two step or not
                    if (message.ReceiveTime is null)
                        continue;
                    if (message.PTP_TWO_STEP)
                    {
                        //two step, 记下 sync 报文的精确到达时间 t2; 等待 follow_up 报文收到时处理 t1
                        t2 = message.ReceiveTime;
                    }
                    else if (message.ReceiveTime.GetTotalNanoseconds() - lastSync > syncIntervalMs)
                    {
                        // one step.
                        t2 = message.ReceiveTime;
                        t1 = message.Timestamp;
                        var delay_req = PTPGenerator.DelayReq(Domain, ClockId, req_seq++, 0);
                        await ptpClockEvent.SendAsync(delay_req, new IPEndPoint(IPAddress.Parse(ptpMulticastAddrs[Domain]), 319));

                        //ptpClientEvent.Receive(ref remote);
                        // 记下发送delay_req的时间。
                        t3 = getCorrectedTime();
                    }
                }
                catch (Exception ex)
                {

                }
            }
        }
    }

}
