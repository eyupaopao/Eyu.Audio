using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eyu.Audio.PTP;

public static class MessageType
{
    // Event message
    public const byte SYNC = 0X00;
    public const byte DELAY_REQ = 0x01;
    public const byte PATH_DELAY_REQ = 0x02;
    public const byte PATH_DELAY_RESP = 0x03;
    public static bool IsEvent(int value)
    {
        return value >= SYNC && value <= PATH_DELAY_RESP;
    }
    public static bool IsGeneral(int value)
    {
        return value >= FOLLOW_UP && value <= MANAGEMENT;
    }
    // General message
    public const byte FOLLOW_UP = 0x08;
    public const byte DELAY_RESP = 0x09;
    public const byte PATH_DELAY_FOLLOW_UP = 0x0A;
    public const byte ANNOUNCE = 0x0B;
    public const byte SIGNALING = 0x0C;
    public const byte MANAGEMENT = 0x0D;
}
