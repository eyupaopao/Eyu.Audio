using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Eyu.Audio.AES67;

public static class Aes67Const
{
    public static IPEndPoint SdpMuticastIPEndPoint = new IPEndPoint(IPAddress.Parse(SdpMuticastAddress), SdpMuticastPort);
    public const string SdpMuticastAddress = "239.255.255.255";
    public const int SdpMuticastPort = 9875;
    public const int Aes67MuticastPort = 5004;
    public const string Deletion = "Deletion";
    public const string Announcement = "Announcement";
    public static uint DefaultPTimeμs = 250;
    public static string DefaultEncoding => $"L{DefaultBitsPerSample}";
    public static int DefaultBitsPerSample = 24;
    public static int DefaultSampleRate = 48000;
    public static int DefaultPayloadType = 96;
}
