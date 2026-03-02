using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Eyu.Audio.PTP;

public class PTPClock
{
    static string[] ptpMulticastAddrs = ["224.0.1.129", "224.0.1.130", "224.0.1.131", "224.0.1.132"];
    UdpClient ptpClockEvent;
    UdpClient ptpClockGeneral;

    public byte Domain { get; private set; }
    IPAddress domainAddress => IPAddress.Parse(ptpMulticastAddrs[Domain]);

    public byte[] ClockId { get; private set; }

    byte _priority1 = 128;
    public byte Priority1
    {
        get => _priority1;
        set { _priority1 = value; if (IsRunning) RunBmca(); }
    }

    byte _priority2 = 128;
    public byte Priority2
    {
        get => _priority2;
        set { _priority2 = value; if (IsRunning) RunBmca(); }
    }

    public byte ClockClass { get; set; } = 248;
    public byte ClockAccuracy { get; set; } = 0xFE;
    public ushort ClockVariance { get; set; } = 25536;
    public byte TimeSource { get; set; } = 0xA0;

    long lastSync = 0;
    public bool IsRunning => cts != null && !cts.IsCancellationRequested;

    volatile bool _isMaster = false;
    public bool IsMaster => _isMaster;

    int announceLogInterval = 1;
    double announceIntervalMs => Math.Pow(2, announceLogInterval) * 1000;
    int syncLogInterval = -2;
    double syncIntervalMs => Math.Pow(2, syncLogInterval) * 1000;

    string ptpMaster = "";
    volatile bool _sync = false;
    string addr = "127.0.0.1";

    public bool IsSynced => _sync;
    public string PtpMaster => ptpMaster;

    PTPTimestamp t1, t2, t3, t4;
    public PTPTimestamp Delay { get; private set; }
    public PTPTimestamp Offset { get; private set; } = new PTPTimestamp(0);

    ushort sync_seq = 0;
    ushort req_seq = 0;

    readonly Dictionary<string, ClockIdentity> knownClocks = new();
    readonly object clocksLock = new();
    readonly object sendLock = new();

    public event Action<bool> OnRoleChanged;

    static PTPClock instance;
    public static PTPClock Instance => instance ??= new PTPClock();

    CancellationTokenSource cts;

    public PTPTimestamp Timestamp => new PTPTimestamp(PTPTimmer.TimeStampNanoseconds) - Offset;

    PTPTimestamp getCorrectedTime() => new PTPTimestamp(PTPTimmer.TimeStampNanoseconds) - Offset;

    bool IsOwnMessage(PTPMessage message)
    {
        if (ClockId == null || message.SourcePortIdentity == null || message.SourcePortIdentity.Length < 8)
            return false;
        for (int i = 0; i < 8; i++)
            if (message.SourcePortIdentity[i] != ClockId[i]) return false;
        return true;
    }

    /// <summary>
    /// 开始PTP时钟，启动3个Task：GeneralHandler(UDP 320)、EventHandler(UDP 319)、MasterSender
    /// </summary>
    public void Start(string? addr = null, byte domain = 0, byte priority1 = 128, byte priority2 = 128)
    {
        if (cts != null && !cts.IsCancellationRequested) return;
        cts = new CancellationTokenSource();
        if (!string.IsNullOrEmpty(addr))
            this.addr = addr;
        Domain = domain;
        _priority1 = priority1;
        _priority2 = priority2;
        ClockId = PTPGenerator.GenerateClockId();
        Task.Run(GeneralHandler);
        Task.Run(EventHandler);
        Task.Run(MasterSender);
    }

    public void Stop()
    {
        if (cts == null || cts.IsCancellationRequested) return;
        cts?.Cancel();
        try { ptpClockEvent?.Close(); } catch { }
        try { ptpClockGeneral?.Close(); } catch { }
        lock (clocksLock) { knownClocks.Clear(); }
        _isMaster = false;
        _sync = false;
    }

    #region Handlers

    private void GeneralHandler()
    {
        ptpClockGeneral = new UdpClient(320);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            ptpClockGeneral.JoinMulticastGroup(domainAddress);
        else
            ptpClockGeneral.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                new MulticastOption(domainAddress, IPAddress.Any));

        var remote = new IPEndPoint(IPAddress.Any, 0);
        while (!cts.IsCancellationRequested)
        {
            try
            {
                var buffer = ptpClockGeneral.Receive(ref remote);
                if (buffer.Length < 34) continue;
                var message = new PTPMessage(buffer);
                if (message.Version != 2 || message.Domain != Domain) continue;
                if (IsOwnMessage(message)) continue;

                switch (message.MessageId)
                {
                    case MessageType.FOLLOW_UP:
                        HandleFollowUpMessage(message);
                        break;
                    case MessageType.DELAY_RESP:
                        HandleDelayRespMessage(message);
                        break;
                    case MessageType.ANNOUNCE:
                        HandleAnnounceMessage(message);
                        break;
                }
            }
            catch (SocketException) when (cts.IsCancellationRequested) { break; }
            catch { }
        }
    }

    private void EventHandler()
    {
        ptpClockEvent = new UdpClient(319);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            ptpClockEvent.JoinMulticastGroup(domainAddress);
        else
            ptpClockEvent.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                new MulticastOption(domainAddress, IPAddress.Any));

        var remote = new IPEndPoint(IPAddress.Any, 0);
        while (!cts.IsCancellationRequested)
        {
            try
            {
                var buffer = ptpClockEvent.Receive(ref remote);
                if (buffer.Length < 34) continue;
                var message = new PTPMessage(buffer, getCorrectedTime());
                if (message.Version != 2 || message.Domain != Domain) continue;
                if (IsOwnMessage(message)) continue;

                if (message.MessageId == MessageType.SYNC && !_isMaster)
                {
                    HandleSyncMessage(message);
                }
                else if (message.MessageId == MessageType.DELAY_REQ && _isMaster)
                {
                    HandleDelayReqMessage(message);
                }
            }
            catch (SocketException) when (cts.IsCancellationRequested) { break; }
            catch { }
        }
    }

    #endregion

    #region Slave

    private void HandleSyncMessage(PTPMessage message)
    {
        if (message.SourcePortIdentityString != ptpMaster)
        {
            ptpMaster = message.SourcePortIdentityString;
            _sync = false;
        }
        sync_seq = message.SequencId;
        if (message.ReceiveTime is null) return;

        if (message.PTP_TWO_STEP)
        {
            t2 = message.ReceiveTime;
        }
        else if (message.ReceiveTime.GetTotalNanoseconds() / 1_000_000 - lastSync > syncIntervalMs)
        {
            t2 = message.ReceiveTime;
            t1 = message.Timestamp;
            SendDelayReq();
        }
    }

    private void HandleFollowUpMessage(PTPMessage message)
    {
        if (_isMaster) return;
        if (message.SequencId != sync_seq) return;

        t1 = message.Timestamp;
        var currentTime = getCorrectedTime();
        if (currentTime.GetTotalNanoseconds() / 1_000_000 - lastSync > syncIntervalMs)
        {
            SendDelayReq();
            lastSync = currentTime.GetTotalNanoseconds() / 1_000_000;
        }
    }

    private void HandleDelayRespMessage(PTPMessage message)
    {
        if (_isMaster) return;
        if (message.SequencId != req_seq) return;

        t4 = message.Timestamp;
        Delay = (t4 - t3 + t2 - t1) / 2;
        var offset = (t2 - t1 - t4 + t3) / 2;
        Offset += offset;
        if (!_sync) _sync = true;
    }

    private void SendDelayReq()
    {
        var delay_req = PTPGenerator.DelayReq(Domain, ClockId, req_seq++, 0);
        ptpClockEvent.Send(delay_req, new IPEndPoint(domainAddress, 319));
        t3 = getCorrectedTime();
    }

    #endregion

    #region Master

    /// <summary>
    /// 合并的主时钟发送Task：定时发送Sync+FollowUp和Announce，同时清理过期时钟
    /// </summary>
    private async void MasterSender()
    {
        long lastAnnounceTicks = Environment.TickCount64;
        long lastCleanupTicks = Environment.TickCount64;

        while (!cts.IsCancellationRequested)
        {
            try
            {
                if (_isMaster)
                {
                    SendSyncAndFollowUp();

                    var now = Environment.TickCount64;
                    if (now - lastAnnounceTicks >= (long)announceIntervalMs)
                    {
                        SendAnnounce();
                        lastAnnounceTicks = now;
                    }
                }

                var nowTicks = Environment.TickCount64;
                if (nowTicks - lastCleanupTicks >= (long)announceIntervalMs)
                {
                    CleanExpiredClocks();
                    lastCleanupTicks = nowTicks;
                }

                await Task.Delay((int)syncIntervalMs, cts.Token);
            }
            catch (OperationCanceledException) { break; }
            catch { }
        }
    }

    private void SendSyncAndFollowUp()
    {
        lock (sendLock)
        {
            if (!_isMaster || ptpClockEvent == null || ptpClockGeneral == null) return;
            sync_seq++;

            var syncBuffer = PTPGenerator.Sync(Domain, ClockId, sync_seq, syncLogInterval);
            var syncTimestamp = PTPTimmer.TimeStampNanoseconds;
            ptpClockEvent.Send(syncBuffer, new IPEndPoint(domainAddress, 319));

            var followUpMsg = PTPGenerator.FollowUp(Domain, ClockId, sync_seq, syncLogInterval,
                new PTPTimestamp(syncTimestamp).GetTimestamp());
            ptpClockGeneral.Send(followUpMsg, new IPEndPoint(domainAddress, 320));
        }
    }

    private void SendAnnounce()
    {
        if (ptpClockGeneral == null) return;
        var timestamp = new PTPTimestamp(PTPTimmer.TimeStampNanoseconds).GetTimestamp();
        var announceMsg = PTPGenerator.Announce(Domain, ClockId, announceLogInterval, timestamp,
            37, _priority1, ClockClass, ClockAccuracy, ClockVariance, _priority2, TimeSource);
        ptpClockGeneral.Send(announceMsg, new IPEndPoint(domainAddress, 320));
    }

    /// <summary>
    /// 主时钟处理从时钟的Delay_Req，回复Delay_Resp
    /// </summary>
    private void HandleDelayReqMessage(PTPMessage message)
    {
        if (ptpClockGeneral == null) return;
        var receiveTime = getCorrectedTime();
        var resp = PTPGenerator.DelayResp(Domain, ClockId, message.SequencId,
            receiveTime.GetTimestamp(), message.SourcePortIdentity);
        ptpClockGeneral.Send(resp, new IPEndPoint(domainAddress, 320));
    }

    #endregion

    #region BMCA

    /// <summary>
    /// 处理收到的Announce消息，更新已知时钟列表并触发BMCA
    /// </summary>
    private void HandleAnnounceMessage(PTPMessage message)
    {
        var identity = new ClockIdentity
        {
            ClockId = message.SourcePortIdentityString,
            Priority1 = message.Priority1,
            Priority2 = message.Priority2,
            ClockClass = message.ClockClass,
            ClockAccuracy = message.ClockAccuracy,
            ClockVariance = message.ClockVariance,
            StepsRemoved = message.StepsRemoved,
            TimeSource = message.TimeSource,
            LastReceived = DateTime.UtcNow
        };

        lock (clocksLock)
        {
            knownClocks[identity.ClockId] = identity;
        }

        RunBmca();
    }

    /// <summary>
    /// 清理超过3倍Announce间隔未收到消息的时钟，并在必要时触发BMCA
    /// </summary>
    private void CleanExpiredClocks()
    {
        bool changed = false;
        lock (clocksLock)
        {
            var expireThreshold = TimeSpan.FromMilliseconds(announceIntervalMs * 3);
            var expired = knownClocks
                .Where(kv => DateTime.UtcNow - kv.Value.LastReceived > expireThreshold)
                .Select(kv => kv.Key).ToList();

            foreach (var key in expired)
            {
                knownClocks.Remove(key);
                changed = true;
            }

            if (knownClocks.Count == 0 && !_isMaster)
                changed = true;
        }

        if (changed) RunBmca();
    }

    /// <summary>
    /// IEEE 1588 Best Master Clock Algorithm - 比较自身与所有已知时钟，决定角色
    /// </summary>
    private void RunBmca()
    {
        var ownClockIdStr = ClockId != null
            ? BitConverter.ToString(ClockId).Replace("-", ":") + ":00:00"
            : "";

        var ownIdentity = new ClockIdentity
        {
            ClockId = ownClockIdStr,
            Priority1 = _priority1,
            Priority2 = _priority2,
            ClockClass = ClockClass,
            ClockAccuracy = ClockAccuracy,
            ClockVariance = ClockVariance,
            StepsRemoved = 0,
            TimeSource = TimeSource,
            LastReceived = DateTime.UtcNow
        };

        var bestClock = ownIdentity;
        lock (clocksLock)
        {
            foreach (var clock in knownClocks.Values)
            {
                if (clock.CompareTo(bestClock) < 0)
                    bestClock = clock;
            }
        }

        bool shouldBeMaster = ReferenceEquals(bestClock, ownIdentity);
        if (shouldBeMaster == _isMaster) return;

        _isMaster = shouldBeMaster;
        if (_isMaster)
        {
            _sync = true;
            Offset = new PTPTimestamp(0);
            ptpMaster = "";
        }
        else
        {
            _sync = false;
            ptpMaster = bestClock.ClockId;
        }

        OnRoleChanged?.Invoke(_isMaster);
    }

    #endregion
}

/// <summary>
/// 时钟标识类，用于BMC算法
/// </summary>
internal class ClockIdentity : IComparable<ClockIdentity>
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

    /// <summary>
    /// IEEE 1588 BMCA 比较顺序: Priority1 > ClockClass > ClockAccuracy > ClockVariance > Priority2 > ClockIdentity
    /// 返回负数表示 this 更优（应为 Master）
    /// </summary>
    public int CompareTo(ClockIdentity other)
    {
        if (other == null) return -1;
        int result = Priority1.CompareTo(other.Priority1);
        if (result != 0) return result;
        result = ClockClass.CompareTo(other.ClockClass);
        if (result != 0) return result;
        result = ClockAccuracy.CompareTo(other.ClockAccuracy);
        if (result != 0) return result;
        result = ClockVariance.CompareTo(other.ClockVariance);
        if (result != 0) return result;
        result = Priority2.CompareTo(other.Priority2);
        if (result != 0) return result;
        return string.Compare(ClockId, other.ClockId, StringComparison.Ordinal);
    }
}
