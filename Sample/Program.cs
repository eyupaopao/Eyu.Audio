// See https://aka.ms/new-console-template for more information
using Eyu.Audio;
using Eyu.Audio.Aes67;
using Eyu.Audio.Alsa;
using Eyu.Audio.Reader;
using Eyu.Audio.Recorder;
using Eyu.Audio.Timer;
using Eyu.Audio.Utils;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Sample;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;


TestAes67();
//PulseCapture.OpenCancel();
//var device = PulseCapture.GetDevices();
//if (device.Count() == 0)
//{
//    Console.WriteLine("no device");
//    return;
//}
//await SdlIn(args);
// var pa = new PulseCapture(device.First(d => d.Name == "echocancel"));
// pa.DataAvailable += Pa_DataAvailable;
// pa.StartRecording();
// Console.ReadLine();
// pa.StopRecording();

// void Pa_DataAvailable(object? sender, WaveInEventArgs e)
// {
//     Console.WriteLine($"capture {e.BytesRecorded} bytes");
// }

static void SdlOut(string[] args)
{
    Console.WriteLine("press any key to start");
    Console.ReadLine();
    var devices = SdlApi.GetDevices(0);
    foreach (var device in devices)
    {
        Console.WriteLine(device);
    }
    Console.WriteLine($"input index to select device");
    var key = Console.ReadLine();

    var flag = int.TryParse(key, out var num);
    if (!flag || num < 0 || num >= devices.Count)
    {
        Console.WriteLine("error input");
        num = -1;
    }

    var file = "";
    if (args.Length == 0)
        file = "D:\\User\\Music\\F.I.R\\飞儿乐团 - 我们的爱.mp3";
    else file = args[0];
    var audioReader = new Eyu.Audio.Reader.AudioFileReader(file);
    if (audioReader == null || !audioReader.CanRead) return;
    var sdlout = new SDLOut(num == -1 ? null : devices[num]);
    sdlout.Init(audioReader);
    sdlout.Play();

    Console.WriteLine("press any key to pause");
    Console.ReadLine();

    sdlout.Pause();

    Console.WriteLine("press any key to play");
    Console.ReadLine();

    sdlout.Play();
    Console.WriteLine("press any key to stop");
    Console.ReadLine();

    sdlout.Stop();
}

static async Task SdlIn(string[] args)
{
    var sdlin = new SDLCapture();
    sdlin.WaveFormat = new WaveFormat(44100, 16, 2);
    var stream = File.OpenWrite("test.wav");
    sdlin.DataAvailable += (sender, e) =>
    {
        stream.Write(e.Buffer, 0, e.BytesRecorded);
        // Console.WriteLine($"capture {e.BytesRecorded} bytes");
    };
    sdlin.StartRecording();
    await Task.Delay(10000);
    sdlin.StopRecording();
    stream.Close();

}

static void AlsaRecord(string[] args)
{
    var alsa = new AlsaCapture(args[0]);
    //alsa.DataAvailable += Alsa_DataAvailable;
    alsa.StartRecording(args[1]);
}

static void TestAes67()
{
    var network = GetNetWorkInfo();
    int index = 1;
    foreach (var item in network)
    {
        Console.WriteLine($"输入序号选择网络：{index++}:{item}");
    }
    var key = Console.ReadKey();
    var flag = int.TryParse(key.KeyChar.ToString(), out index);
    if (flag && index <= network.Count)
    {
        var address = IPAddress.Parse(network[index - 1]);
        Aes67Manager aes67Manager = new Aes67Manager(address);
        Console.ReadLine();
    }
}

static List<string> GetNetWorkInfo()
{
    List<string> netWorkList = new();
    NetworkInterface[] NetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
    //获取所有的网络接口
    foreach (NetworkInterface NetworkIntf in NetworkInterfaces)                         //针对每张网卡
    {
        IPInterfaceProperties IPInterfaceProperties = NetworkIntf.GetIPProperties();    //获取描述此网络接口的配置的对象
        UnicastIPAddressInformationCollection UnicastIPAddressInformationCollection = IPInterfaceProperties.UnicastAddresses;//获取分配给此接口的单播地址
        foreach (UnicastIPAddressInformation UnicastIPAddressInformation in UnicastIPAddressInformationCollection) //针对每个IP
        {
            if (UnicastIPAddressInformation.Address.AddressFamily == AddressFamily.InterNetwork)//IPv4
            {
                string IP = UnicastIPAddressInformation.Address.ToString();
                if (IP != "127.0.0.1")//不是本地IP
                {
                    if (NetworkIntf.OperationalStatus == OperationalStatus.Up)//网卡已连接
                    {
                        netWorkList.Add(IP);
                    }
                }
            }
        }
    }
    //var ip = NetworkInterfaces[1].GetIPProperties();
    return netWorkList;
    //Net.AddRange(NetworkInterfaces);
}
