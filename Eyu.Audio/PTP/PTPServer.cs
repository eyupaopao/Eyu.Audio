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
    public class PTPServer
    {
        // PTP v2 组播地址
        static string[] ptpMulticastAddrs = new string[] { "224.0.1.129", "224.0.1.130", "224.0.1.131", "224.0.1.132" };

        UdpClient ptpServerEvent;
        UdpClient ptpServerGeneral;

        // PTP 设置项
        public byte Domain { get; private set; }
        IPAddress domainAddress => IPAddress.Parse(ptpMulticastAddrs[Domain]);

        // 服务器配置
        public string ServerId { get; private set; }
        public byte Priority1 { get; set; } = 128;  // 优先级1，数值越小优先级越高
        public byte Priority2 { get; set; } = 128;  // 优先级2
        public byte ClockClass { get; set; } = 248; // 时钟等级
        public byte ClockAccuracy { get; set; } = 0xFE; // 时钟精度
        public ushort ClockVariance { get; set; } = 25536; // 时钟方差
        public byte TimeSource { get; set; } = 0xA0; // 时间源

        // 服务器状态
        bool isRunning = false;
        bool isMaster = true; // 默认作为主时钟
        int announceLogInterval = 1; // announce消息间隔指数 时间间隔为2的指数次方秒
        int syncLogInterval = -2; // sync消息间隔指数 时间间隔为2的指数次方秒
        int sync_seq = 0;
        private object syncLock = new object(); // 用于同步sync和follow-up操作

        // BMC算法相关
        private Dictionary<string, ClockIdentity> knownClocks = new Dictionary<string, ClockIdentity>();
        private ClockIdentity bestMasterClock = null;
        private object clockLock = new object();

        // 本机地址
        string addr = "127.0.1";

        static PTPServer instance;
        public static PTPServer Instance => instance ??= new PTPServer();

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
            GenerateServerId();
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
            ptpServerEvent = new UdpClient(319); // Event port
            ptpServerGeneral = new UdpClient(320); // General port

            // 加入组播组
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ptpServerEvent.JoinMulticastGroup(domainAddress);
                ptpServerGeneral.JoinMulticastGroup(domainAddress);
            }
            else
            {
                ptpServerEvent.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(domainAddress, IPAddress.Any));
                ptpServerGeneral.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(domainAddress, IPAddress.Any));
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

            ptpServerEvent?.Close();
            ptpServerGeneral?.Close();
        }

        /// <summary>
        /// 生成服务器ID
        /// </summary>
        private void GenerateServerId()
        {
            // 使用网络接口的MAC地址生成唯一ID
            try
            {
                var macAddress = GetMacAddress();
                ServerId = BitConverter.ToString(macAddress).Replace("-", "").ToLower();
            }
            catch
            {
                // 如果获取MAC地址失败，使用随机ID
                ServerId = Guid.NewGuid().ToString("N").Substring(0, 16).ToLower();
            }
        }

        /// <summary>
        /// 获取本机MAC地址
        /// </summary>
        private byte[] GetMacAddress()
        {
            var networkInterfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
            foreach (var ni in networkInterfaces.Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up && n.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback))
            {
                var mac = ni.GetPhysicalAddress().GetAddressBytes();
                if (mac.Length == 6 && mac.Any(b => b != 0))
                    return mac;
            }

            // 如果没有找到合适的网络接口，返回默认值
            return new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55 };
        }

        /// <summary>
        /// 获取PTP时间戳
        /// </summary>
        private byte[] GetPtpTimestamp()
        {
            return PTPTimmer.GetTimestamp();
        }

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
                sync_seq = (sync_seq + 1) % 0x10000;

                var syncBuffer = PTPGenerator.Sync(Domain, GetServerIdBytes(), syncLogInterval);

                // 记录发送Sync消息的精确时间戳
                var syncTimestamp = PTPTimmer.TimeStampNanoseconds;

                // 发送Sync消息
                ptpServerEvent.Send(syncBuffer, new IPEndPoint(domainAddress, 319));

                // 立即发送Follow-Up消息包含Sync消息的准确发送时刻
                var followUpMsg = CreateFollowUpMessageWithTimestamp(syncTimestamp, sync_seq);
                ptpServerGeneral.Send(followUpMsg, new IPEndPoint(domainAddress, 320));
            }
        }

        /// <summary>
        /// 将字符串转换为字节数组
        /// </summary>
        private byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        /// <summary>
        /// 获取服务器ID字节数组
        /// </summary>
        private byte[] GetServerIdBytes()
        {
            var idBytes = new byte[8];
            var serverIdBytes = StringToByteArray(ServerId.PadRight(16, '0').Substring(0, 16));
            Array.Copy(serverIdBytes, idBytes, 8);
            return idBytes;
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
                        var announceMsg = PTPGenerator.Announce(Domain, GetServerIdBytes(), announceLogInterval, GetPtpTimestamp(), Priority1, ClockClass, ClockAccuracy, ClockVariance, Priority2, TimeSource);
                        ptpServerGeneral.Send(announceMsg, new IPEndPoint(domainAddress, 320));
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
        /// 通用消息处理器
        /// </summary>
        private void GeneralHandler()
        {
            var remote = new IPEndPoint(IPAddress.Any, 0);
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    var buffer = ptpServerGeneral.Receive(ref remote);
                    if (buffer.Length < 31) continue;

                    var message = new PTPMessage(buffer);

                    // 检查版本和域
                    if (message.Version != 2 || message.Domain != Domain)
                        continue;

                    // 处理收到的PTP消息，用于BMC算法
                    ProcessReceivedMessage(message, remote);
                }
                catch (Exception ex)
                {
                    // 忽略异常，继续运行
                }
            }
        }

        /// <summary>
        /// 事件消息处理器
        /// </summary>
        private void EventHandler()
        {
            var remote = new IPEndPoint(IPAddress.Any, 0);
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    var buffer = ptpServerEvent.Receive(ref remote);
                    if (buffer.Length < 31) continue;

                    var message = new PTPMessage(buffer);

                    // 检查版本和域
                    if (message.Version != 2 || message.Domain != Domain)
                        continue;

                    // 处理收到的事件消息
                    // 对于DELAY_REQ消息，需要获取精确接收时间
                    if (message.MessageId == MessageType.DELAY_REQ)
                    {
                        // 记录收到DELAY_REQ的精确时间戳
                        var receiveTimestamp = PTPTimmer.TimeStampNanoseconds;
                        HandleDelayReqMessageWithTimestamp(message, remote, receiveTimestamp);
                    }
                    else
                    {
                        ProcessReceivedMessage(message, remote);
                    }
                }
                catch (Exception ex)
                {
                    // 忽略异常，继续运行
                }
            }
        }

        /// <summary>
        /// 处理收到的消息，用于BMC算法
        /// </summary>
        private void ProcessReceivedMessage(PTPMessage message, IPEndPoint remoteEndpoint)
        {
            // 根据消息类型处理
            switch (message.MessageId)
            {
                case MessageType.ANNOUNCE:
                    HandleAnnounceMessage(message, remoteEndpoint);
                    break;
                case MessageType.SYNC:
                    HandleSyncMessage(message, remoteEndpoint);
                    break;
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

        /// <summary>
        /// 处理Sync消息
        /// </summary>
        private void HandleSyncMessage(PTPMessage message, IPEndPoint remoteEndpoint)
        {
            if (!isMaster) // 作为从时钟时才处理Sync消息
            {
                // 发送Follow_Up消息
                var followUpMsg = CreateFollowUpMessage(message.Timestamp, message.SequencId);
                ptpServerGeneral.Send(followUpMsg, new IPEndPoint(domainAddress, 320));
            }
        }

        /// <summary>
        /// 处理Delay_Req消息
        /// </summary>
        private void HandleDelayReqMessage(PTPMessage message, IPEndPoint remoteEndpoint)
        {
            if (isMaster) // 作为主时钟时才处理Delay_Req消息
            {
                // 发送Delay_Resp消息
                var delayRespMsg = CreateDelayRespMessage(message.SequencId, message.SourcePortIdentity, PTPTimmer.TimeStampNanoseconds);
                ptpServerGeneral.Send(delayRespMsg, remoteEndpoint);
            }
        }

        /// <summary>
        /// 处理Delay_Req消息（带接收时间戳）
        /// </summary>
        private void HandleDelayReqMessageWithTimestamp(PTPMessage message, IPEndPoint remoteEndpoint, ulong receiveTimestamp)
        {
            if (isMaster) // 作为主时钟时才处理Delay_Req消息
            {
                // 发送Delay_Resp消息，包含收到Delay_Req的精确时间
                var delayRespMsg = CreateDelayRespMessage(message.SequencId, message.SourcePortIdentity, receiveTimestamp);
                ptpServerGeneral.Send(delayRespMsg, remoteEndpoint);
            }
        }

        /// <summary>
        /// 创建Follow_Up消息
        /// </summary>
        private byte[] CreateFollowUpMessage(PTPTimestamp syncTimestamp, int sequenceId)
        {
            var length = 44;
            var buffer = new byte[length];

            // 消息类型
            buffer[0] = (byte)(Eyu.Audio.PTP.MessageType.FOLLOW_UP & 0x0F);
            buffer[1] = 2; // version

            // 消息长度
            buffer[2] = (byte)(length >> 8);
            buffer[3] = (byte)(length & 0xff);

            // 域号
            buffer[4] = (byte)Domain;

            // 源端口标识符
            var serverIdBytes = GetServerIdBytes();
            Array.Copy(serverIdBytes, 0, buffer, 20, 8);
            buffer[28] = 0; // 端口号高字节
            buffer[29] = 1; // 端口号低字节

            // 序列号
            buffer[30] = (byte)(sequenceId >> 8);
            buffer[31] = (byte)(sequenceId & 0xff);

            // 控制域
            buffer[32] = 0x02; // Follow_Up

            // 时间戳
            var timestamp = GetPtpTimestamp();
            Array.Copy(timestamp, 0, buffer, 34, 10);

            return buffer;
        }

        /// <summary>
        /// 创建Follow_Up消息（使用ulong时间戳）
        /// </summary>
        private byte[] CreateFollowUpMessageWithTimestamp(ulong syncTimestamp, int sequenceId)
        {
            var length = 44;
            var buffer = new byte[length];

            // 消息类型
            buffer[0] = (byte)(Eyu.Audio.PTP.MessageType.FOLLOW_UP & 0x0F);
            buffer[1] = 2; // version

            // 消息长度
            buffer[2] = (byte)(length >> 8);
            buffer[3] = (byte)(length & 0xff);

            // 域号
            buffer[4] = (byte)Domain;

            // 标志位 - 保持与Sync消息一致
            buffer[6] = (byte)(Eyu.Audio.PTP.FlagField.PTP_TWO_STEP >> 8);
            buffer[7] = (byte)(Eyu.Audio.PTP.FlagField.PTP_TWO_STEP & 0xFF);

            // 保留字段（CorrectionField）
            Array.Clear(buffer, 8, 8);

            // 源端口标识符
            var serverIdBytes = GetServerIdBytes();
            Array.Copy(serverIdBytes, 0, buffer, 20, 8);
            buffer[28] = 0; // 端口号高字节
            buffer[29] = 1; // 端口号低字节

            // 序列号
            buffer[30] = (byte)(sequenceId >> 8);
            buffer[31] = (byte)(sequenceId & 0xff);

            // 控制域
            buffer[32] = 0x02; // Follow_Up

            // 消息间隔
            buffer[33] = 0x00; // LogMeanMessageInterval

            // 时间戳 - 使用sync消息的精确发送时间
            var timestamp = new PTPTimestamp(syncTimestamp);
            var timestampBytes = timestamp.GetTimestamp();
            Array.Copy(timestampBytes, 0, buffer, 34, 10);

            return buffer;
        }



        /// <summary>
        /// 创建Delay_Resp消息
        /// </summary>
        private byte[] CreateDelayRespMessage(int requestSequenceId, byte[] requestingPortIdentity, ulong receiveTimestamp)
        {
            var length = 54;
            var buffer = new byte[length];

            // 消息类型
            buffer[0] = (byte)(Eyu.Audio.PTP.MessageType.DELAY_RESP & 0x0F);
            buffer[1] = 2; // version

            // 消息长度
            buffer[2] = (byte)(length >> 8);
            buffer[3] = (byte)(length & 0xff);

            // 域号
            buffer[4] = (byte)Domain;

            // 标志位 - 保持与Sync消息一致
            buffer[6] = (byte)(Eyu.Audio.PTP.FlagField.PTP_TWO_STEP >> 8);
            buffer[7] = (byte)(Eyu.Audio.PTP.FlagField.PTP_TWO_STEP & 0xFF);

            // 保留字段（CorrectionField）
            Array.Clear(buffer, 8, 8);

            // 源端口标识符
            var serverIdBytes = GetServerIdBytes();
            Array.Copy(serverIdBytes, 0, buffer, 20, 8);
            buffer[28] = 0; // 端口号高字节
            buffer[29] = 1; // 端口号低字节

            // 序列号
            buffer[30] = (byte)(requestSequenceId >> 8);
            buffer[31] = (byte)(requestSequenceId & 0xff);

            // 控制域
            buffer[32] = 0x03; // Delay_Resp

            // 消息间隔
            buffer[33] = 0x00; // LogMeanMessageInterval

            // 时间戳 - 使用收到Delay_Req的精确时间
            var timestamp = new PTPTimestamp(receiveTimestamp);
            var timestampBytes = timestamp.GetTimestamp();
            Array.Copy(timestampBytes, 0, buffer, 34, 10);

            // 请求端口标识符
            Array.Copy(requestingPortIdentity, 0, buffer, 44, requestingPortIdentity.Length);

            return buffer;
        }

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
                        ClockId = ServerId,
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
                        ClockId = ServerId,
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
        /// 发送Sync消息（主时钟功能）
        /// </summary>
        public void SendSyncMessage()
        {
            if (!isMaster) return;

            var length = 44;
            var buffer = new byte[length];

            sync_seq = (sync_seq + 1) % 0x10000;

            // 消息类型
            buffer[0] = (byte)(Eyu.Audio.PTP.MessageType.SYNC & 0x0F); // type
            buffer[1] = 2; // version

            // 消息长度
            buffer[2] = (byte)(length >> 8);
            buffer[3] = (byte)(length & 0xff);

            // 域号
            buffer[4] = (byte)Domain;

            // 标志位 - 设置为两步时钟
            buffer[6] = (byte)(Eyu.Audio.PTP.FlagField.PTP_TWO_STEP >> 8);
            buffer[7] = (byte)(Eyu.Audio.PTP.FlagField.PTP_TWO_STEP & 0xFF);

            // 保留字段（CorrectionField）
            Array.Clear(buffer, 8, 8);

            // 源端口标识符
            var serverIdBytes = GetServerIdBytes();
            Array.Copy(serverIdBytes, 0, buffer, 20, 8);
            buffer[28] = 0; // 端口号高字节
            buffer[29] = 1; // 端口号低字节

            // 序列号
            buffer[30] = (byte)(sync_seq >> 8);
            buffer[31] = (byte)(sync_seq & 0xff);

            // 控制域
            buffer[32] = 0x00; // Sync

            // 消息间隔
            buffer[33] = 0x00; // LogMeanMessageInterval

            // 发送消息
            ptpServerEvent.Send(buffer, new IPEndPoint(domainAddress, 319));
        }

        public bool IsMaster => isMaster;
        public bool IsRunning => isRunning;

        CancellationTokenSource cts;
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
