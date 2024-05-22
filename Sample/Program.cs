// See https://aka.ms/new-console-template for more information
using Eyu.Audio;
using Eyu.Audio.Alsa;
using Eyu.Audio.Reader;
using Eyu.Audio.Recorder;
using Eyu.Audio.Utils;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;


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
IntPtr hints = IntPtr.Zero;
IntPtr name = IntPtr.Zero;
if (InteropAlsa.snd_device_name_hint(-1, "pcm", hints) < 0)
{
    Console.WriteLine($"Cannot get device names");
}

Console.ReadLine();
//SdlIn(args);


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

static void AlsaRecord()
{
    var alsa = new AlsaCapture();
    alsa.StartRecording();
    Console.ReadLine();
}
