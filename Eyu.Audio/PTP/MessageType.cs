using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eyu.Audio.PTP;

public static class MessageType
{
    // Event message
    public const int SYNC = 0X00;
    public const int DELAY_REQ = 0x01;
    public const int PATH_DELAY_REQ = 0x02;
    public const int PATH_DELAY_RESP = 0x03;
    public static bool IsEvent(int value)
    {
        return value >= SYNC && value <= PATH_DELAY_RESP;
    }
    public static bool IsGeneral(int value)
    {
        return value >= FOLLOW_UP && value <= MANAGEMENT;
    }
    // General message
    public const int FOLLOW_UP = 0x08;
    public const int DELAY_RESP = 0x09;
    public const int PATH_DELAY_FOLLOW_UP = 0x0A;
    public const int ANNOUNCE = 0x0B;
    public const int SIGNALING = 0x0C;
    public const int MANAGEMENT = 0x0D;
}
