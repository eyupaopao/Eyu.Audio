using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Eyu.Audio.PTP
{
    public class PTPClock
    {
        // PTP v2 组播地址
        static string[] ptpMulticastAddrs = new string[] { "224.0.1.129", "224.0.1.130", "224.0.1.131", "224.0.1.132" };

        UdpClient ptpClockEvent;
        UdpClient ptpClockGeneral;
        /// <summary>
        /// 默认同步模式为两步模式
        /// </summary>
        public int SyncType = FlagField.PTP_TWO_STEP;
        // PTP 设置项
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

        // 最小同步间隔 ms 
        long syncInterval;
        // 最近一次同步时间(ms)
        long lastSync = 0;
        public bool IsMaster => isMaster;
        public bool IsRunning => isRunning;

        CancellationTokenSource cts;

        // 服务器状态
        bool isRunning = false;
        bool isMaster = true; // 默认作为主时钟
        int announceLogInterval = 1; // announce消息间隔指数 时间间隔为2的指数次方秒
        int syncLogInterval = -2; // sync消息间隔指数 时间间隔为2的指数次方秒
        // sync 报文 id
        ushort sync_seq = 0;
        // delay_req 报文 id
        ushort req_seq = 0;
        private object syncLock = new object(); // 用于同步sync和follow-up操作

        // BMC算法相关
        private Dictionary<string, ClockIdentity> knownClocks = new Dictionary<string, ClockIdentity>();
        private ClockIdentity bestMasterClock = null;
        private object clockLock = new object();

        // 本机地址
        string addr = "127.0.1";
        // 参与计算的各个时间戳
        PTPTimestamp t1, t2, t3, t4;

        static PTPClock instance;
        public static PTPClock Instance => instance ??= new PTPClock();
        public PTPTimestamp Timestamp
        {
            get
            {
                return new PTPTimestamp(PTPTimmer.TimeStampNanoseconds) - Offset;
            }
        }
        // 本机时间与主时钟时间偏移量
        public PTPTimestamp Offset { get; private set; } = new PTPTimestamp(0);
        public PTPTimestamp Delay { get; private set; }
        /// <summary>
        /// 初始化PTP服务器
        /// </summary>
        /// <param name="addr">服务器地址</param>
        /// <param name="domain">PTP域</param>
        /// <param name="priority1">优先级1</param>
        /// <param name="priority2">优先级2</param>
        public void Initialize(string? addr = null, byte domain = 0, byte priority1 = 128, byte priority2 = 128)
        {
            if (!string.IsNullOrEmpty(addr))
            {
                this.addr = addr;
            }
            Domain = domain;
            Priority1 = priority1;
            Priority2 = priority2;

            // 生成服务器ID（使用MAC地址或其他唯一标识）
            ClockId = PTPGenerator.GenerateClockId();
        }

        /// <summary>
        /// 开始PTP服务器
        /// </summary>
        public void Start()
        {
            if (isRunning) return;

            isRunning = true;
            cts = new CancellationTokenSource();

            // 创建UDP客户端
            ptpClockEvent = new UdpClient(319); // Event port
            ptpClockGeneral = new UdpClient(320); // General port

            // 加入组播组
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ptpClockEvent.JoinMulticastGroup(domainAddress);
                ptpClockGeneral.JoinMulticastGroup(domainAddress);
            }
            else
            {
                ptpClockEvent.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(domainAddress, IPAddress.Any));
                ptpClockGeneral.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(domainAddress, IPAddress.Any));
            }

            // 启动处理任务
            Task.Run(AnnounceSender);
            Task.Run(SyncSender);
            Task.Run(GeneralHandler);
            Task.Run(EventHandler);
        }

        public void Stop()
        {
            if (!isRunning) return;

            isRunning = false;
            cts?.Cancel();

            ptpClockEvent?.Close();
            ptpClockGeneral?.Close();
        }

        #region master
        /// <summary>
        /// 发送Sync消息（主时钟功能）- 定时发送Sync包，发送完Sync包发送FollowUp包
        /// </summary>
        private async void SyncSender()
        {
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    if (isMaster)
                    {
                        // 发送Sync消息并立即发送Follow-Up消息
                        SendSyncAndFollowUp();
                    }

                    await Task.Delay((int)Math.Pow(2, syncLogInterval), cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // 忽略异常，继续运行
                }
            }
        }

        /// <summary>
        /// 发送Sync消息和Follow-Up消息
        /// </summary>
        private void SendSyncAndFollowUp()
        {
            lock (syncLock)
            {
                if (!isMaster) return;

                // 生成序列号
                sync_seq = sync_seq++;

                var syncBuffer = PTPGenerator.Sync(Domain, ClockId, sync_seq, syncLogInterval);

                // 记录发送Sync消息的精确时间戳
                var syncTimestamp = PTPTimmer.TimeStampNanoseconds;

                // 发送Sync消息
                ptpClockEvent.Send(syncBuffer, new IPEndPoint(domainAddress, 319));

                // 立即发送Follow-Up消息包含Sync消息的准确发送时刻
                var followUpMsg = PTPGenerator.FollowUp(Domain, ClockId, sync_seq, syncLogInterval, new PTPTimestamp(syncTimestamp).GetTimestamp());
                ptpClockGeneral.Send(followUpMsg, new IPEndPoint(domainAddress, 320));
            }
        }

        /// <summary>
        /// 处理Delay_Req消息（带接收时间戳）
        /// </summary>
        private void HandleDelayReqMessage(PTPMessage message, IPEndPoint remoteEndpoint, ulong receiveTimestamp)
        {


        }
        #endregion

        #region slaver

        #endregion

        #region control

        /// <summary>
        /// 运行BMC（Best Master Clock）算法
        /// </summary>
        private void RunBMCAlgorithm()
        {
            lock (clockLock)
            {
                // 移除超过一定时间未收到消息的时钟
                var expiredClocks = knownClocks.Where(kvp =>
                    DateTime.UtcNow - kvp.Value.LastReceived > TimeSpan.FromSeconds(5)).Select(kvp => kvp.Key).ToList();

                var clockId = BitConverter.ToString(ClockId).Replace("-", "").ToLower();
                foreach (var expiredClock in expiredClocks)
                {
                    knownClocks.Remove(expiredClock);
                }

                // 如果当前没有主时钟或者有更优的时钟，则重新选择
                if (knownClocks.Count == 0)
                {
                    // 没有其他时钟，自己作为主时钟
                    isMaster = true;
                    bestMasterClock = new ClockIdentity
                    {
                        ClockId = clockId,
                        Priority1 = Priority1,
                        Priority2 = Priority2,
                        ClockClass = ClockClass,
                        ClockAccuracy = ClockAccuracy,
                        ClockVariance = ClockVariance,
                        StepsRemoved = 0,
                        TimeSource = TimeSource,
                        LastReceived = DateTime.UtcNow
                    };
                    return;
                }

                // 找到最佳时钟
                var currentBest = knownClocks.Values
                    .OrderBy(c => c.Priority1)
                    .ThenBy(c => c.ClockClass)
                    .ThenBy(c => c.Priority2)
                    .ThenBy(c => c.ClockAccuracy)
                    .FirstOrDefault();

                if (currentBest != null)
                {
                    // 比较当前最佳时钟和自己
                    var myClock = new ClockIdentity
                    {
                        ClockId = clockId,
                        Priority1 = Priority1,
                        Priority2 = Priority2,
                        ClockClass = ClockClass,
                        ClockAccuracy = ClockAccuracy,
                        ClockVariance = ClockVariance,
                        StepsRemoved = 0,
                        TimeSource = TimeSource,
                        LastReceived = DateTime.UtcNow
                    };

                    var comparison = CompareClocks(currentBest, myClock);

                    if (comparison < 0)
                    {
                        // 其他时钟更优，切换到从模式
                        isMaster = false;
                        bestMasterClock = currentBest;
                    }
                    else if (comparison > 0)
                    {
                        // 自己更优，保持主模式或切换到主模式
                        isMaster = true;
                        bestMasterClock = myClock;
                        Offset = new PTPTimestamp(0); // 重置偏移量
                    }
                    // 如果相等，保持当前状态
                }
            }
        }

        /// <summary>
        /// 比较两个时钟的优劣
        /// </summary>
        /// <returns>负数表示clock1更优，正数表示clock2更优，0表示相等</returns>
        private int CompareClocks(ClockIdentity clock1, ClockIdentity clock2)
        {
            // 按照PTP标准的BMC算法比较步骤：
            // 1. Priority1
            if (clock1.Priority1 != clock2.Priority1)
                return clock1.Priority1.CompareTo(clock2.Priority1);

            // 2. ClockClass
            if (clock1.ClockClass != clock2.ClockClass)
                return clock1.ClockClass.CompareTo(clock2.ClockClass);

            // 3. Priority2
            if (clock1.Priority2 != clock2.Priority2)
                return clock1.Priority2.CompareTo(clock2.Priority2);

            // 4. ClockAccuracy
            if (clock1.ClockAccuracy != clock2.ClockAccuracy)
                return clock1.ClockAccuracy.CompareTo(clock2.ClockAccuracy);

            // 5. Variance (derived from clockVariance)
            var variance1 = clock1.ClockVariance;
            var variance2 = clock2.ClockVariance;
            if (variance1 != variance2)
                return variance1.CompareTo(variance2);

            // 6. Steps Removed
            if (clock1.StepsRemoved != clock2.StepsRemoved)
                return clock1.StepsRemoved.CompareTo(clock2.StepsRemoved);

            // 7. ClockIdentity (MAC address based)
            return string.Compare(clock1.ClockId, clock2.ClockId, StringComparison.Ordinal);
        }

        /// <summary>
        /// 发送Announce消息
        /// </summary>
        private async void AnnounceSender()
        {
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    if (isMaster)
                    {
                        // 发送Announce消息
                        var announceMsg = PTPGenerator.Announce(Domain, ClockId, announceLogInterval, GetPtpTimestamp(), Priority1, ClockClass, ClockAccuracy, ClockVariance, Priority2, TimeSource);
                        ptpClockGeneral.Send(announceMsg, new IPEndPoint(domainAddress, 320));
                    }

                    await Task.Delay((int)Math.Pow(2, announceLogInterval), cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // 忽略异常，继续运行
                }
            }
        }
        /// <summary>
        /// 处理Announce消息
        /// </summary>
        private void HandleAnnounceMessage(PTPMessage message, IPEndPoint remoteEndpoint)
        {
            // 解析Announce消息字段
            message.ParseAnnounceFields(message.RawData);

            // 提取时钟标识
            var clockId = BitConverter.ToString(message.SourcePortIdentity, 0, 8).Replace("-", "").ToLower();
            var clockIdentity = new ClockIdentity
            {
                ClockId = clockId,
                Priority1 = message.Priority1,
                Priority2 = message.Priority2,
                ClockClass = message.ClockClass,
                ClockAccuracy = message.ClockAccuracy,
                ClockVariance = message.ClockVariance,
                StepsRemoved = message.StepsRemoved,
                TimeSource = message.TimeSource,
                LastReceived = DateTime.UtcNow
            };

            // 更新已知时钟列表
            lock (clockLock)
            {
                knownClocks[clockId] = clockIdentity;
            }

            // 运行BMC算法
            RunBMCAlgorithm();
        }

        #endregion
        /// <summary>
        /// 获取PTP时间戳
        /// </summary>
        private byte[] GetPtpTimestamp()
        {
            return PTPTimmer.GetTimestamp();
        }


        /// <summary>
        /// 控制消息处理器，处理Follow_Up、Delay_Resp，Announce
        /// </summary>
        private void GeneralHandler()
        {
            var remote = new IPEndPoint(IPAddress.Any, 0);
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    var buffer = ptpClockGeneral.Receive(ref remote);
                    if (buffer.Length < 31) continue;

                    var message = new PTPMessage(buffer);

                    // 检查版本和域
                    if (message.Version != 2 || message.Domain != Domain)
                        continue;

                    // 处理收到的PTP消息
                    // 根据消息类型处理
                    switch (message.MessageId)
                    {
                        case MessageType.ANNOUNCE:
                            HandleAnnounceMessage(message, remote);
                            break;
                        case MessageType.FOLLOW_UP:
                            if (isMaster || sync_seq != message.SequencId || getCorrentedTime().GetTotalNanoseconds() / 1000000 - lastSync < syncInterval) break;
                            // sync 报文的精确发送时间 t1
                            t1 = message.Timestamp;
                            // 发送delay_req 报文
                            var delay_req = PTPGenerator.DelayReq(Domain, ClockId, req_seq++, 0);
                            ptpClockEvent.Send(delay_req, new IPEndPoint(IPAddress.Parse(ptpMulticastAddrs[Domain]), 319));
                            // 记录delay_req报文发送的时间。
                            t3 = getCorrentedTime();
                            break;
                        case MessageType.DELAY_RESP:
                            if (isMaster || req_seq != message.SequencId) break;
                            t4 = message.Timestamp;
                            // 计算偏移量和延迟
                            Delay = (t4 - t3 + t2 - t1) / 2;
                            var offset = ((t2 - t1) - (t4 - t3)) / 2;
                            Offset += offset;
                            lastSync = getCorrentedTime().GetTotalNanoseconds() / 1000_000;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    // 忽略异常，继续运行
                }
            }
        }

        /// <summary>
        /// 事件消息处理器，包括Sync和Delay_Req消息
        /// </summary>
        private void EventHandler()
        {
            var remote = new IPEndPoint(IPAddress.Any, 0);
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    var buffer = ptpClockEvent.Receive(ref remote);
                    if (buffer.Length < 31) continue;

                    var message = new PTPMessage(buffer);

                    // 检查版本和域
                    if (message.Version != 2 || message.Domain != Domain)
                        continue;

                    // 处理收到的事件消息
                    // 对于DELAY_REQ消息，需要获取精确接收时间
                    switch (message.MessageId)
                    {
                        case MessageType.DELAY_REQ:
                            // 作为主时钟时处理DELAY_REQ消息
                            if (!isMaster) break;
                            // 记录收到DELAY_REQ的精确时间戳
                            var receiveTimestamp = PTPTimmer.TimeStampNanoseconds;
                            // 发送Delay_Resp消息，包含收到Delay_Req的精确时间
                            var delayRespMsg = PTPGenerator.DelayResp(Domain, ClockId, message.SequencId, new PTPTimestamp(receiveTimestamp).GetTimestamp(), message.SourcePortIdentity);
                            ptpClockGeneral.Send(delayRespMsg, remote);
                            break;
                        case MessageType.SYNC:
                            _ = HandleSync(message);
                            // 作为从时钟时处理Sync消息
                            if (isMaster) break;
                            // 记录收到Sync消息的时间戳
                            t2 = new PTPTimestamp(PTPTimmer.TimeStampNanoseconds);
                            break;
                    }

                }
                catch (Exception ex)
                {
                    // 忽略异常，继续运行
                }
            }
        }
        PTPTimestamp getCorrentedTime()
        {
            return new PTPTimestamp(PTPTimmer.TimeStampNanoseconds) - Offset;
        }

        private async Task HandleSync(PTPMessage message)
        {
            // 是不是新的master时钟
            if (message.SourcePortIdentityString != bestMasterClock.ClockId)
            {
                // 新主时钟处理
            }
            //save sequence number
            sync_seq = message.SequencId;
            if (message.ReceiveTime is null)
                return;
            //check if master is two step or not
            if (message.PTP_TWO_STEP)
            {
                //two step, 记下 sync 报文的精确到达时间 t2; 等待 follow_up 报文收到时处理 t1
                t2 = message.ReceiveTime;
            }
            else if (message.ReceiveTime.GetTotalNanoseconds() - lastSync > syncInterval)
            {
                // one step, 记下 t1, t2, t3
                t2 = message.ReceiveTime;
                t1 = message.Timestamp;
                var delay_req = PTPGenerator.DelayReq(Domain, ClockId, req_seq++, 0);
                await ptpClockEvent.SendAsync(delay_req, new IPEndPoint(IPAddress.Parse(ptpMulticastAddrs[Domain]), 319));
                t3 = getCorrentedTime();
            }
        }

    }

    /// <summary>
    /// 时钟标识类，用于BMC算法
    /// </summary>
    internal class ClockIdentity
    {
        public string ClockId { get; set; }
        public byte Priority1 { get; set; }
        public byte Priority2 { get; set; }
        public byte ClockClass { get; set; }
        public byte ClockAccuracy { get; set; }
        public ushort ClockVariance { get; set; }
        public byte StepsRemoved { get; set; }
        public byte TimeSource { get; set; }
        public DateTime LastReceived { get; set; }
    }
}
