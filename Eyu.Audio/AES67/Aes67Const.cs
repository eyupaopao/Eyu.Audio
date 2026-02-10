using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Eyu.Audio.Aes67;

public static class Aes67Const
{
    public static IPEndPoint SdpMulticastIPEndPoint = new IPEndPoint(IPAddress.Parse(SdpMulticastAddress), SdpMulticastPort);
    public const string SdpMulticastAddress = "239.255.255.255";
    public const int SdpMulticastPort = 9875;
    public const int Aes67MuticastPort = 5004;
    public const string Deletion = "Deletion";
    public const string Announcement = "Announcement";
    public const uint DefaultPTimeμs = 250;
    public static string DefaultEncoding => $"L{DefaultBitsPerSample}";
    public static int DefaultBitsPerSample = 24;
    public static int DefaultChannels = 2;
    public static int DefaultSampleRate = 48000;
    public static int DefaultPayloadType = 96;
    /// <summary>
    /// AES67要求支持的采样率: 44.1kHz, 48kHz, 88.2kHz, 96kHz, 176.4kHz, 192kHz
    /// </summary>
    public static int[] SupportedSampleRates = [44100, 48000, 88200, 96000, 176400, 192000];
    public static int[] SupportedBitsPerSample = [16,24,32];
    public static uint[] SupportedPTimeμs = [125, 250, 333, 1000, 4000];
}
