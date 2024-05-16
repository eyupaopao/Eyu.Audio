// See https://aka.ms/new-console-template for more information
using Eyu.Audio;

using Eyu.Audio.Reader;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;


var file = "";
if (args.Length == 0)
    file = "D:\\User\\Music\\F.I.R\\飞儿乐团 - 我们的爱.mp3";
else file = args[0];
var audioReader = new Eyu.Audio.Reader.AudioFileReader(file);
if (audioReader == null || !audioReader.CanRead) return;
var sampleToWave16 = new SampleToWaveProvider16(audioReader);
var sdlout = new SDLOut();
sdlout.Init(sampleToWave16);
sdlout.Play();

Console.ReadLine();

sdlout.Pause();

Console.ReadLine();

sdlout.Play();

Console.ReadLine();

sdlout.Stop();