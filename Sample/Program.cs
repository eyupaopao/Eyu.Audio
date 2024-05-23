﻿// See https://aka.ms/new-console-template for more information
using Eyu.Audio;
using Eyu.Audio.Alsa;
using Eyu.Audio.Reader;
using Eyu.Audio.Recorder;
using Eyu.Audio.Utils;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Sample;
using System.Diagnostics;

var device = PulseCapture.GetDevices();
if(device.Count() == 0)
{
    Console.WriteLine("no device");
    return;
}
var pa = new PulseCapture(device.First());
pa.DataAvailable+= Pa_DataAvailable;
pa.StartRecording();
Console.ReadLine();
pa.StopRecording();

void Pa_DataAvailable(object? sender, WaveInEventArgs e)
{
    Console.WriteLine($"capture {e.BytesRecorded} bytes");
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
