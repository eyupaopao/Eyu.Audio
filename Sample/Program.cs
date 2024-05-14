// See https://aka.ms/new-console-template for more information
using Eyu.Audio.Alsa;
using Eyu.Audio.Provider;
using Eyu.Audio.Reader;
using Eyu.Audio.Utils;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

//var audioReader = new Mp3Reader("D:\\User\\Music\\F.I.R\\飞儿乐团 - 我们的爱.mp3");

var alsa = AlsaDeviceBuilder.Create(new SoundDeviceSettings());
alsa.Play("/home/swan/飞儿乐团 - 我们的爱.mp3");


Console.ReadLine();