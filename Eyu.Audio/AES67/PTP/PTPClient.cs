using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Eyu.Audio
{
    public class PTPClient
    {
        // ptp v2 组播地址
        static string[] ptpMulticastAddrs = new string[] { "224.0.1.129", "224.0.1.130", "224.0.1.131", "224.0.1.132" };
        UdpClient ptpClientEvent;
        UdpClient ptpClientGeneral;

        // ptp 设置项
        // 选择一个地址
        public int Domain { get; private set; }
        IPAddress domainAddress => IPAddress.Parse(ptpMulticastAddrs[Domain]);
        // 当前主机id
        string ptpMaster = "";
        bool sync = false;
        // 本机地址
        string addr = "127.0.0.1";
        // 最小同步间隔 ms 
        long syncInterval;

        // 参数
        // 参与计算的各个时间戳
        PTPTimestamp t1, t2, t3, t4;

        public PTPTimestamp Delay { get; private set; }

        // 本机时间与服务器时间偏移量
        public PTPTimestamp Offset { get; private set; } = new PTPTimestamp(0);
        // sync 报文 id
        int sync_seq = 0;
        // delay_req 报文 id
        int req_seq = 0;
        // 最近一次同步时间(ms)
        long lastSync = 0;
        static PTPClient instance;
        public static PTPClient Instance => instance ??= new PTPClient();

        /// <summary>
        /// 开始同步时间
        /// </summary>
        /// <param Name="addr">使用的地址</param>
        /// <param Name="domain">选择PTP域</param>
        /// <param Name="callback">连接成功回调函数</param>
        /// <param Name="syncInterval">同步间隔</param>
        public void Start(string? addr = null, int domain = 0, uint syncInterval = 300)
        {
            if (!string.IsNullOrEmpty(addr))
            {
                this.addr = addr;
            }
            Domain = domain;

            this.syncInterval = syncInterval;
            cts = new CancellationTokenSource();
            Task.Run(ptpClientGeneralHandler);
            Task.Run(ptpEventHandler);
        }



        public bool IsSynced => sync;
        public bool IsMaster { get; private set; } = false;
        public string PtpMaster => ptpMaster;

        public PTPTimestamp UtcNow
        {
            get
            {
                return new PTPTimestamp(PTPTimmer.UtcNowNanoseconds) - Offset;
            }
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
            ptpClientEvent.Close();
            ptpClientGeneral.Close();
        }
        /// <summary>
        /// 构建delay_req
        /// </summary>
        /// <returns></returns>
        byte[] ptp_delay_req()
        {
            var length = 52;
            var buffer = new byte[length];
            // 每次获取都生成一个新的id，小于 0x10000;
            req_seq = (req_seq + 1) % 0x10000;
            buffer[0] = MessageType.DELAY_REQ;// type
            buffer[1] = 2;// version
            // 写长度 messagelenght
            buffer[2] = (byte)(length >> 8);
            buffer[3] = (byte)(length & 0xff);
            // 写id 
            buffer[30] = (byte)(req_seq >> 8);
            buffer[31] = (byte)(req_seq & 0xff);
            return buffer;
        }

        // 获取与ptp服务器对时后的时间戳
        PTPTimestamp getCorrentedTime()
        {
            return new PTPTimestamp(PTPTimmer.TimeStampNanoseconds) - Offset;
        }

        CancellationTokenSource cts;


        private void ptpClientGeneralHandler()
        {
            ptpClientGeneral = new UdpClient(320);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ptpClientGeneral.JoinMulticastGroup(domainAddress);
            }
            else
            {
                ptpClientGeneral.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(domainAddress, IPAddress.Any));
            }
            var remote = new IPEndPoint(IPAddress.Any, 0);
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    var buffer = ptpClientGeneral.Receive(ref remote);
                    if (buffer.Length < 31) continue;
                    var message = new PTPMessage(buffer);
                    var source = BitConverter.ToString(message.SourcePortIdentity[0..8]).ToLower() + ":0";
                    var sourceAlt = BitConverter.ToString(message.SourcePortIdentity).Replace('-', ':').ToLower();
                    // 检查版本和连接地址
                    if (message.Version != 2 || message.Domain != Domain)
                        continue;
                    // follo_up 报文
                    if (message.MessageId == MessageType.FOLLOW_UP && sync_seq == message.SequencId && getCorrentedTime().GetTotalNanoseconds() / 1000000 - lastSync > syncInterval)
                    {
                        // sync 报文的精确发送时间 t1
                        t1 = message.Timestamp;
                        // 发送delay_req报文
                        ptpClientEvent.Send(ptp_delay_req(), new IPEndPoint(IPAddress.Parse(ptpMulticastAddrs[Domain]), 319));
                        // 记录delay_req报文发送的时间。
                        t3 = getCorrentedTime();
                    }
                    // delay_resp报文：
                    else if (message.MessageId == MessageType.DELAY_RESP && req_seq == message.SequencId)
                    {
                        // 获取主时钟收到delay_req的时间。
                        t4 = message.Timestamp;
                        // 计算延迟。
                        Delay = (t4 - t3 + t2 - t1) / 2;
                        var offset = (t2 - t1 - t4 + t3) / 2;
                        Offset += offset;
                        //if (Debugger.IsAttached)
                        //    Console.WriteLine($"同步：offset {offset}ns; delay {delay}ns；结果：{Offset}");
                        lastSync = getCorrentedTime().GetTotalNanoseconds() / 1000_000;
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


        private async void ptpEventHandler()
        {

            ptpClientEvent = new UdpClient(319);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ptpClientEvent.JoinMulticastGroup(domainAddress);
            }
            else
            {
                ptpClientEvent.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(domainAddress, IPAddress.Any));
            }
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    var remote = new IPEndPoint(IPAddress.Any, 0);
                    var buffer = ptpClientEvent.Receive(ref remote);
                    var recv_ts = getCorrentedTime();
                    if (buffer.Length < 31) continue;
                    var message = new PTPMessage(buffer);
                    var source = BitConverter.ToString(message.SourcePortIdentity).ToLower() + ":0";
                    var sourceAlt = BitConverter.ToString(message.SourcePortIdentity).Replace('-', ':').ToLower();
                    // 检查版本和广播域
                    if (message.Version != 2 || message.Domain != Domain)
                        continue;
                    //只处理 sync 消息
                    if (message.MessageId != MessageType.SYNC)
                        continue;
                    // 是不是新的master时钟
                    if (source != ptpMaster)
                    {
                        // 新时钟
                        ptpMaster = source;
                        // 从新同步
                        sync = false;
                    }

                    //save sequence number
                    sync_seq = message.SequencId;
                    var timestamp = getCorrentedTime().GetTotalNanoseconds();
                    //check if master is two step or not
                    if (message.PTP_TWO_STEP)
                    {
                        //two step, 记下 sync 报文的精确到达时间 t2; 等待 follow_up 报文收到时处理 t1
                        t2 = recv_ts;
                    }
                    else if (timestamp - lastSync > syncInterval)
                    {
                        // one step.
                        t2 = recv_ts;
                        t1 = message.Timestamp;
                        var delay_req = ptp_delay_req();
                        await ptpClientEvent.SendAsync(delay_req, new IPEndPoint(IPAddress.Parse(ptpMulticastAddrs[Domain]), 319));

                        //ptpClientEvent.Receive(ref remote);
                        // 记下发送delay_req的时间。
                        t3 = getCorrentedTime();
                    }
                }
                catch (Exception ex)
                {

                }
            }
        }
    }

}
