using Eyu.Audio;
using NAudio.Wave;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace Sample;

/// <summary>
/// PulseLoopbackCapture 测试（仅 Linux，依赖 PulseAudio/PipeWire）。
/// </summary>
public static class PulseLoopbackCaptureTest
{
    public static void TestPulseLoopbackCapture()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Console.WriteLine("PulseLoopbackCapture 仅支持 Linux，当前平台跳过。");
            return;
        }

        Console.WriteLine("=== 测试 PulseLoopbackCapture（系统播放环回） ===");
        Console.WriteLine("请先播放一段音乐或视频，然后按回车开始采集...");
        Console.ReadLine();

        try
        {
            string? defaultMonitor = PulseLoopbackCapture.GetDefaultMonitorSourceName();
            string? pactlMonitor = PulseLoopbackCapture.GetDefaultMonitorSourceNameFromPactl();
            Console.WriteLine($"Monitor 源: {defaultMonitor} ({pactlMonitor ?? "pactl 未解析"})");

            using var capture = new PulseLoopbackCapture(monitorSourceName: null, audioBufferMillisecondsLength: 100);
            capture.WaveFormat = new WaveFormat(48000, 16, 2);

            int totalBytes = 0;
            int chunkCount = 0;

            capture.DataAvailable += (_, e) =>
            {
                Interlocked.Add(ref totalBytes, e.BytesRecorded);
                Interlocked.Increment(ref chunkCount);
            };

            capture.RecordingStopped += (_, e) =>
            {
                if (e.Exception != null)
                    Console.WriteLine($"采集停止（异常）: {e.Exception.Message}");
                else
                    Console.WriteLine("采集已停止。");
            };

            Console.WriteLine("开始环回采集，约 20 秒后自动停止（或按回车提前停止）...");
            capture.StartRecording();

            for (int i = 0; i < 20; i++)
            {
                if (Console.KeyAvailable)
                {
                    Console.ReadKey(true);
                    break;
                }
                Thread.Sleep(1000);
                int bytes = Volatile.Read(ref totalBytes);
                int chunks = Volatile.Read(ref chunkCount);
                Console.WriteLine($"  {20 - i} 秒... 已采集 {bytes} 字节（{chunks} 次回调）");
            }

            capture.StopRecording();
            Thread.Sleep(300);

            Console.WriteLine($"=== 完成: 共 {chunkCount} 次回调, {totalBytes} 字节 ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"错误: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    public static void TestPulseLoopbackCaptureToFile()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Console.WriteLine("PulseLoopbackCapture 仅支持 Linux，当前平台跳过。");
            return;
        }

        Console.WriteLine("=== 测试 PulseLoopbackCapture 写入 WAV 文件 ===");
        Console.WriteLine("请先播放一段音乐或视频，然后按回车开始采集...");
        Console.ReadLine();

        const string fileName = "test_pulse_loopback.wav";
        try
        {
            using var capture = new PulseLoopbackCapture(monitorSourceName: null, audioBufferMillisecondsLength: 100);
            capture.WaveFormat = new WaveFormat(48000, 16, 2);

            using var writer = new WaveFileWriter(fileName, capture.WaveFormat);
            int totalBytes = 0;

            capture.DataAvailable += (_, e) =>
            {
                writer.Write(e.Buffer, 0, e.BytesRecorded);
                Interlocked.Add(ref totalBytes, e.BytesRecorded);
            };

            capture.RecordingStopped += (_, e) =>
            {
                if (e.Exception != null)
                    Console.WriteLine($"采集停止（异常）: {e.Exception.Message}");
            };

            Console.WriteLine($"开始环回采集并写入 {fileName}，约 20 秒...");
            capture.StartRecording();

            for (int i = 0; i < 20; i++)
            {
                if (Console.KeyAvailable) { Console.ReadKey(true); break; }
                Thread.Sleep(1000);
                int bytes = Volatile.Read(ref totalBytes);
                Console.WriteLine($"  {20 - i} 秒... 已采集 {bytes} 字节");
            }

            capture.StopRecording();
            System.Threading.Thread.Sleep(400);

            if (File.Exists(fileName))
            {
                var fi = new FileInfo(fileName);
                Console.WriteLine($"已保存: {fileName}, {fi.Length} 字节");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"错误: {ex.Message}");
        }
    }
}
