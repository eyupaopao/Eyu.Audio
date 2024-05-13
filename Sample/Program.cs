// See https://aka.ms/new-console-template for more information
using Eyu.Audio.Provider;
using Eyu.Audio.Reader;
using Eyu.Audio.Utils;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

var audioReader = new Mp3Reader("D:\\User\\Music\\F.I.R\\飞儿乐团 - 我们的爱.mp3");
var buffer = new byte[1024 * 1024];
Wave32To16Stream
while (true)
{

    var len = audioReader.Read(buffer);
    Console.WriteLine(len);
    if (len == 0)
        break;
}
