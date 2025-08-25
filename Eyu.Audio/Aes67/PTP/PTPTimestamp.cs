using System;

public class PTPTimestamp
{
    public const long NanosecondsPerSecond = 1000000000;
    public long Seconds { get; private set; }
    public long Nanoseconds { get; private set; }

    // 构造函数保持不变...
    public PTPTimestamp(long seconds, long nanoseconds)
    {
        Seconds = seconds;
        Nanoseconds = nanoseconds;
        Normalize();
    }

    //public PTPTimestamp(params long[] timeArray)
    //{
    //    if (timeArray == null || timeArray.Length != 2)
    //        throw new ArgumentException("数组必须包含2个元素（秒和纳秒）", nameof(timeArray));

    //    Seconds = timeArray[0];
    //    Nanoseconds = timeArray[1];
    //    Normalize();
    //}

    public PTPTimestamp(ulong totalNanoseconds)
    {
        Seconds = (long)(totalNanoseconds / NanosecondsPerSecond);
        Nanoseconds = (long)(totalNanoseconds % NanosecondsPerSecond);
        Normalize();
    }

    private void Normalize()
    {
        if (Nanoseconds >= NanosecondsPerSecond)
        {
            long carry = Nanoseconds / NanosecondsPerSecond;
            Seconds += carry;
            Nanoseconds %= NanosecondsPerSecond;
        }
        else if (Nanoseconds < 0)
        {
            long borrow = (Math.Abs(Nanoseconds) + NanosecondsPerSecond - 1) / NanosecondsPerSecond;
            Seconds -= borrow;
            Nanoseconds += borrow * NanosecondsPerSecond;
        }
    }

    // 修复：使用long类型处理总纳秒数，支持负数
    public long GetTotalNanoseconds()
    {
        return Seconds * NanosecondsPerSecond + Nanoseconds;
    }

    // 相等性运算符
    public static bool operator ==(PTPTimestamp a, PTPTimestamp b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;

        return a.GetTotalNanoseconds() == b.GetTotalNanoseconds();
    }

    public static bool operator !=(PTPTimestamp a, PTPTimestamp b)
    {
        return !(a == b);
    }

    // 小于运算符
    public static bool operator <(PTPTimestamp a, PTPTimestamp b)
    {
        if (a is null) throw new ArgumentNullException(nameof(a));
        if (b is null) throw new ArgumentNullException(nameof(b));

        return a.GetTotalNanoseconds() < b.GetTotalNanoseconds();
    }

    // 大于运算符
    public static bool operator >(PTPTimestamp a, PTPTimestamp b)
    {
        if (a is null) throw new ArgumentNullException(nameof(a));
        if (b is null) throw new ArgumentNullException(nameof(b));

        return a.GetTotalNanoseconds() > b.GetTotalNanoseconds();
    }

    public static bool operator <=(PTPTimestamp a, PTPTimestamp b)
    {
        return a < b || a == b;
    }

    public static bool operator >=(PTPTimestamp a, PTPTimestamp b)
    {
        return a > b || a == b;
    }

    public override bool Equals(object obj)
    {
        return obj is PTPTimestamp timestamp && this == timestamp;
    }

    public override int GetHashCode()
    {
        return GetTotalNanoseconds().GetHashCode();
    }

    // 其他运算符保持不变...
    public static PTPTimestamp operator +(PTPTimestamp a, PTPTimestamp b)
    {
        if (a == null) throw new ArgumentNullException(nameof(a));
        if (b == null) throw new ArgumentNullException(nameof(b));

        return new PTPTimestamp(a.Seconds + b.Seconds, a.Nanoseconds + b.Nanoseconds);
    }

    public static PTPTimestamp operator -(PTPTimestamp a, PTPTimestamp b)
    {
        if (a == null) throw new ArgumentNullException(nameof(a));
        if (b == null) throw new ArgumentNullException(nameof(b));

        return new PTPTimestamp(a.Seconds - b.Seconds, a.Nanoseconds - b.Nanoseconds);
    }

    public static PTPTimestamp operator /(PTPTimestamp a, int b)
    {
        if (a == null) throw new ArgumentNullException(nameof(a));
        if (b == 0) throw new DivideByZeroException("除数不能为零");

        long secondsQuotient = a.Seconds / b;
        long secondsRemainder = a.Seconds % b;
        long totalNanoseconds = secondsRemainder * NanosecondsPerSecond + a.Nanoseconds;
        long nanosecondsQuotient = totalNanoseconds / b;
        long carryOverSeconds = nanosecondsQuotient / NanosecondsPerSecond;

        return new PTPTimestamp(secondsQuotient + carryOverSeconds,
                               nanosecondsQuotient % NanosecondsPerSecond);
    }

    public override string ToString()
    {
        return $"{Seconds}秒 {Nanoseconds}纳秒";
    }
}
