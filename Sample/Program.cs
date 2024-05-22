// See https://aka.ms/new-console-template for more information
using Eyu.Audio;
using Eyu.Audio.Alsa;
using Eyu.Audio.Reader;
using Eyu.Audio.Recorder;
using Eyu.Audio.Utils;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Sample;
using System.Diagnostics;


//var device = SDLOut.GetDeviceNames(0).ToList();
//Console.ReadLine();
//SdlApi.RenderDeviceChanged += SDLOut_RenderDeviceChanged;

//void SDLOut_RenderDeviceChanged(object? sender, IEnumerable<SDLDevice> e)
//{
//    foreach (var device in e)
//    {
//        Console.WriteLine(device.Name);
//    }
//}

//AlsaRecord();
//SdlIn(args);
string recordDevices = RunCommand("pactl list sources short");
Console.WriteLine("设备列表:\n");
var lines = recordDevices.Split('\n');
List<AudioDevice> devices = new List<AudioDevice>();
foreach (var line in lines)
{
    if (string.IsNullOrEmpty(line)) continue;
    var deviceInfo = line.Split('\t');
    if (deviceInfo.Length < 3) continue;
    if (deviceInfo[0] == "0") continue;
    var formatInfo = deviceInfo[3].Split(' ');
    var device = new AudioDevice { id = deviceInfo[0], name = deviceInfo[1], SampleRate = formatInfo[2], Channels = formatInfo[1], Fortmat = formatInfo[0] };
    if (device.name.Contains("input"))
    {
        device.IsCapture = true;
    }
    devices.Add(device);
}
Console.WriteLine("设备列表:\n" + recordDevices);
//string playbackDevices = GetAlsaDevices("aplay -l");
//Console.WriteLine("播放设备列表:\n" + playbackDevices);

//Console.WriteLine($"当前选中设备：{args[0]},写入录音文件{args[1]}");
Console.ReadLine();

//AlsaRecord(args);
//Console.WriteLine("开始录音，按回车结束录音");
//Console.ReadLine();

PulseAudioRecorder recorder = new PulseAudioRecorder();
recorder.RecordAudio("output.raw", 10); // Record for 10 seconds
Console.WriteLine("Recording complete.");
//IntPtr hints = IntPtr.Zero;
//IntPtr name = IntPtr.Zero;
//if (InteropAlsa.snd_device_name_hint(-1, "pcm", hints) < 0)
//{
//    Console.WriteLine($"Cannot get device names");
//}

//Console.ReadLine();
// 

static string RunCommand(string command)
{
    try
    {
        ProcessStartInfo startInfo = new ProcessStartInfo {
            FileName = "/bin/bash",
            Arguments = "-c \"" + command + "\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process process = new Process { StartInfo = startInfo };
        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }
    catch (Exception e)
    {
        return "Error executing command: " + e.Message;
    }
}

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

static void SdlIn(string[] args)
{
    var sdlin = new SDLCapture();
    sdlin.WaveFormat = new WaveFormat(48000, 16, 1);
    sdlin.StartRecording();
    Console.ReadLine();
    sdlin.StopRecording();

}

static void AlsaRecord(string[] args)
{
    var alsa = new AlsaCapture(args[0]);
    //alsa.DataAvailable += Alsa_DataAvailable;
    alsa.StartRecording(args[1]);
}
class AudioDevice
{
    public string id
    {
        get; set;
    }
    public string name
    {
        get; set;
    }
    public string SampleRate
    {
        get; set;
    }
    public string Channels
    {
        get; set;
    }
    public string Fortmat
    {
        get; set;
    }
    public bool IsCapture
    {
        get; set;
    }
}