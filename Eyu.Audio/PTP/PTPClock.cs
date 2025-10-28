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
        static string[] ptpMulticastAddrs = ["224.0.1.129", "224.0.1.130", "224.0.1.131", "224.0.1.132"];

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

        // 最近一次同步时间(ms)
        long lastSync = 0;
        public bool IsMaster => isMaster;
        public bool IsRunning => isRunning;

        CancellationTokenSource cts;

        // 服务器状态
        bool isRunning = false;
        bool isMaster = true; // 默认作为主时钟
        int announceLogInterval = 1; // announce消息间隔指数 时间间隔为2的指数次方秒
        double announceIntervalMs => Math.Pow(2, announceLogInterval) * 1000;
        int syncLogInterval = -2; // sync消息间隔指数 时间间隔为2的指数次方秒
        double syncIntervalMs => Math.Pow(2, syncLogInterval) * 1000;
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

                    await Task.Delay((int)syncIntervalMs, cts.Token);
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

        #endregion

        #region slaver

        /// <summary>
        /// 处理Sync消息 - 从时钟的主要同步逻辑
        /// </summary>
        private async Task HandleSync(PTPMessage message)
        {
            // Only process sync if we're in slave mode
            if (isMaster) return;

            // 获取发送Sync消息的时钟ID
            var sourceClockId = BitConverter.ToString(message.SourcePortIdentity).Replace("-", "").ToLower();

            // 检查是否为新的主时钟（在已知时钟列表中查找）
            ClockIdentity sourceClockIdentity = null;
            lock (clockLock)
            {
                if (knownClocks.ContainsKey(sourceClockId))
                {
                    sourceClockIdentity = knownClocks[sourceClockId];
                    sourceClockIdentity.LastReceived = DateTime.UtcNow; // 更新最后接收时间
                }
            }

            // 如果源时钟不在已知时钟列表中，直接返回（不处理未知时钟的Sync消息）
            if (sourceClockIdentity == null)
            {
                return;
            }

            // 比较新主时钟和当前最佳主时钟（使用BMC算法比较逻辑）
            bool isNewMasterBetter = false;
            if (bestMasterClock != null)
            {
                // 使用BMC算法比较新主时钟和当前主时钟
                var comparison = CompareClocks(sourceClockIdentity, bestMasterClock);
                isNewMasterBetter = comparison < 0; // 如果sourceClock更优，则comparison < 0
            }
            else
            {
                // 如果没有当前主时钟，将此源时钟视为更优
                isNewMasterBetter = true;
            }

            // 根据比较结果决定是否切换主时钟
            if (isNewMasterBetter)
            {
                // 新主时钟更优，从时钟应切换到"Listening"状态，准备同步到新主时钟
                // 在实际实现中，这里可以设置一个中间状态，但在简化实现中，我们直接更新bestMasterClock
                lock (clockLock)
                {
                    bestMasterClock = sourceClockIdentity;
                }

                // 重置同步参数，准备与新主时钟同步
                ResetSyncParameters();

                // 更新sync序列号
                sync_seq = message.SequencId;
                if (message.ReceiveTime is null)
                    return;
                
                // Check if master is two step or not
                if (message.PTP_TWO_STEP)
                {
                    // Two step: record sync arrival time t2; wait for follow-up to get t1
                    t2 = message.ReceiveTime;
                }
                else if (message.ReceiveTime.GetTotalNanoseconds() - lastSync > syncIntervalMs)
                {
                    // One step: record t1 and t2, then send delay request
                    t2 = message.ReceiveTime;
                    t1 = message.Timestamp;
                    var delay_req = PTPGenerator.DelayReq(Domain, ClockId, req_seq++, 0);
                    await ptpClockEvent.SendAsync(delay_req, new IPEndPoint(IPAddress.Parse(ptpMulticastAddrs[Domain]), 319));
                    t3 = getCorrectedTime();
                    lastSync = message.ReceiveTime.GetTotalNanoseconds() / 1000_00;
                }
            }
            else
            {
                // 新主时钟不优于当前主时钟，丢弃该Sync包，不进行任何状态切换
                // 保持与原主时钟的同步
                return;
            }
        }

        /// <summary>
        /// 重置同步参数
        /// </summary>
        private void ResetSyncParameters()
        {
            // 重置时间戳
            t1 = t2 = t3 = t4 = null;
            // 重置序列号相关参数
            // sync_seq 和 req_seq 保持不变，因为它们会在处理消息时更新
        }

        /// <summary>
        /// 处理Follow-Up消息 - 从时钟获取Sync消息的精确发送时间
        /// </summary>
        private void HandleFollowUpMessage(PTPMessage message)
        {
            // Only slave processes Follow-Up messages
            if (isMaster) return;

            // Ensure Follow-Up message matches the most recent Sync message (by sequence number)
            if (message.SequencId == sync_seq)
            {
                // Get the precise send timestamp of the Sync message
                t1 = message.Timestamp;

                // Check if it's time to send a delay request to calculate network delay
                var currentTime = getCorrectedTime();
                if (currentTime.GetTotalNanoseconds() - lastSync > syncIntervalMs)
                {
                    // Send Delay_Req message to calculate network delay
                    var delay_req = PTPGenerator.DelayReq(Domain, ClockId, req_seq++, 0);
                    ptpClockEvent.Send(delay_req, new IPEndPoint(IPAddress.Parse(ptpMulticastAddrs[Domain]), 319));
                    t3 = getCorrectedTime();
                    lastSync = currentTime.GetTotalNanoseconds() / 1000_000;
                }
            }
        }

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
                    bool wasMaster = isMaster;
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
                    
                    // 如果状态发生变化，执行相应操作
                    if (!wasMaster)
                    {
                        OnMasterStateChanged(true);
                    }
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

                    bool wasMaster = isMaster;
                    if (comparison < 0)
                    {
                        // 其他时钟更优，切换到从模式
                        isMaster = false;
                        bestMasterClock = currentBest;
                        
                        // 如果从主时钟变为从时钟，需要执行相应操作
                        if (wasMaster)
                        {
                            OnMasterStateChanged(false);
                        }
                    }
                    else if (comparison > 0)
                    {
                        // 自己更优，保持主模式或切换到主模式
                        isMaster = true;
                        bestMasterClock = myClock;
                        Offset = new PTPTimestamp(0); // 重置偏移量
                        
                        // 如果从未主时钟变为主时钟，需要执行相应操作
                        if (!wasMaster)
                        {
                            OnMasterStateChanged(true);
                        }
                    }
                    // 如果相等，保持当前状态
                }
            }
        }

        /// <summary>
        /// 当主从状态发生变化时调用
        /// </summary>
        /// <param name="isNowMaster">是否现在是主时钟</param>
        private void OnMasterStateChanged(bool isNowMaster)
        {
            if (isNowMaster)
            {
                // 切换到主时钟模式
                // 重置同步参数
                Offset = new PTPTimestamp(0);
                Delay = new PTPTimestamp(0);
                // 重置时间戳
                t1 = t2 = t3 = t4 = null;
            }
            else
            {
                // 切换到从时钟模式
                // 重置时间戳
                t1 = t2 = t3 = t4 = null;
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
                        var announceMsg = PTPGenerator.Announce(Domain, ClockId, announceLogInterval, GetPtpTimestamp(), 37, Priority1, ClockClass, ClockAccuracy, ClockVariance, Priority2, TimeSource, 0); // 使用默认UTC偏移值37秒（2015年7月1日之后的闰秒数），StepsRemoved为0
                        ptpClockGeneral.Send(announceMsg, new IPEndPoint(domainAddress, 320));
                    }

                    await Task.Delay((int)announceIntervalMs, cts.Token);
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
                            HandleFollowUpMessage(message);
                            break;
                        case MessageType.DELAY_RESP:
                            if (isMaster || req_seq != message.SequencId) break;
                            t4 = message.Timestamp;
                            // 计算偏移量和延迟
                            Delay = (t4 - t3 + t2 - t1) / 2;
                            var offset = ((t2 - t1) - (t4 - t3)) / 2;
                            Offset += offset;
                            lastSync = getCorrectedTime().GetTotalNanoseconds() / 1000_000;
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
        PTPTimestamp getCorrectedTime()
        {
            return new PTPTimestamp(PTPTimmer.TimeStampNanoseconds) - Offset;
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
        public ushort StepsRemoved { get; set; }
        public byte TimeSource { get; set; }
        public DateTime LastReceived { get; set; }
    }
}
