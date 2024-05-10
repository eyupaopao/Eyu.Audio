// See https://aka.ms/new-console-template for more information
using Eyu.Audio.Reader;

Console.WriteLine("Hello, World!");

var reader = new Mp3FileReader("D:\\Swan\\IPCAS\\Media\\2024-01-06\\班德瑞 - 寂静山林 - 纯音乐 轻音乐 之 红 天鹅.mp3");
var frame = reader.ReadNextFrame();
Console.WriteLine(frame);