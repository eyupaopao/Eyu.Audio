// See https://aka.ms/new-console-template for more information
using Eyu.Audio;

using Eyu.Audio.Reader;
using Eyu.Audio.Recorder;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;


//SDLOut.GetDeviceNames(1);
//Console.ReadLine();
SdlOut(args);

static void SdlOut(string[] args)
{
    Console.WriteLine("press any key to start");
    Console.ReadLine();
    var file = "";
    if (args.Length == 0)
        file = "D:\\User\\Music\\F.I.R\\飞儿乐团 - 我们的爱.mp3";
    else file = args[0];
    var audioReader = new Eyu.Audio.Reader.AudioFileReader(file);
    if (audioReader == null || !audioReader.CanRead) return;
    var sdlout = new SDLOut();
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
    sdlin.StartRecording();
    Console.ReadLine();
    sdlin.StopRecording();

}