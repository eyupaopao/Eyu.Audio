using Eyu.Audio.Aes67;
using Eyu.Audio.PTP;
using NAudio.Wave;
using System;
using System.Net;
using System.Threading;

namespace Sample;

/// <summary>
/// AES67音频流发送测试程序
/// </summary>
class Aes67SenderTest
{
    private static Aes67Sender? _multicastSender;
    private static Aes67Sender? _unicastSender;
    private static IWaveIn? _waveIn;
    private static PTPClient? _ptpClient;
    private static bool _isRunning = true;

    static void Main(string[] args)
    {
        Console.WriteLine("AES67音频流发送测试程序");
        Console.WriteLine("========================");

        try
        {
            // 初始化PTP客户端
            InitializePTP();

            // 创建音频发送器
            CreateSenders();

            // 开始音频采集
            StartAudioCapture();

            Console.WriteLine("音频流发送已启动...");
            Console.WriteLine("按 ESC 键停止发送");

            // 等待用户输入
            while (_isRunning)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Escape)
                    {
                        break;
                    }
                }
                Thread.Sleep(100);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"错误: {ex.Message}");
        }
        finally
        {
            StopAll();
        }

        Console.WriteLine("程序已退出");
    }

    /// <summary>
    /// 初始化PTP客户端
    /// </summary>
    private static void InitializePTP()
    {
        Console.WriteLine("初始化PTP时间同步客户端...");
        
        _ptpClient = PTPClient.Instance;
        _ptpClient.Start();
        
        Console.WriteLine($"PTP客户端已启动 - 域: {_ptpClient.Domain}");
    }

    /// <summary>
    /// 创建音频发送器
    /// </summary>
    private static void CreateSenders()
    {
        var inputFormat = new WaveFormat(44100, 16, 2); // 输入格式
        var sessionId = (uint)DateTime.Now.Ticks;

        // 创建组播发送器
        try
        {
            var multicastEndpoint = new IPEndPoint(IPAddress.Parse("239.255.255.250"), 5004);
            _multicastSender = new Aes67Sender(
                "AES67组播测试流",
                multicastEndpoint,
                inputFormat,
                sessionId,
                _ptpClient
            );
            _multicastSender.Start();
            Console.WriteLine($"组播发送器已启动 - {multicastEndpoint}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"创建组播发送器失败: {ex.Message}");
        }

        // 创建单播发送器
        try
        {
            var unicastEndpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5006);
            _unicastSender = new Aes67Sender(
                "AES67单播测试流",
                unicastEndpoint,
                inputFormat,
                sessionId + 1,
                _ptpClient
            );
            _unicastSender.Start();
            Console.WriteLine($"单播发送器已启动 - {unicastEndpoint}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"创建单播发送器失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 开始音频采集
    /// </summary>
    private static void StartAudioCapture()
    {
        Console.WriteLine("初始化音频采集设备...");

        // 获取可用的音频设备
        var deviceCount = WaveIn.DeviceCount;
        if (deviceCount == 0)
        {
            throw new Exception("未找到可用的音频采集设备");
        }

        Console.WriteLine($"找到 {deviceCount} 个音频设备:");
        for (int i = 0; i < deviceCount; i++)
        {
            var capabilities = WaveIn.GetCapabilities(i);
            Console.WriteLine($"  [{i}] {capabilities.ProductName}");
        }

        // 使用第一个设备
        _waveIn = new WaveIn();
        _waveIn.DeviceNumber = 0;
        _waveIn.WaveFormat = new WaveFormat(44100, 16, 2);
        _waveIn.BufferMilliseconds = 100;

        // 设置音频数据回调
        _waveIn.DataAvailable += OnDataAvailable;

        // 开始采集
        _waveIn.StartRecording();
        Console.WriteLine("音频采集已开始");
    }

    /// <summary>
    /// 音频数据回调
    /// </summary>
    private static void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        try
        {
            // 发送到组播流
            _multicastSender?.WriteAudio(e.Buffer, 0, e.BytesRecorded);

            // 发送到单播流
            _unicastSender?.WriteAudio(e.Buffer, 0, e.BytesRecorded);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"发送音频数据时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 停止所有发送器
    /// </summary>
    private static void StopAll()
    {
        Console.WriteLine("正在停止发送器...");

        try
        {
            _waveIn?.StopRecording();
            _waveIn?.Dispose();
            Console.WriteLine("音频采集已停止");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"停止音频采集时出错: {ex.Message}");
        }

        try
        {
            _multicastSender?.Stop();
            _multicastSender?.Dispose();
            Console.WriteLine("组播发送器已停止");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"停止组播发送器时出错: {ex.Message}");
        }

        try
        {
            _unicastSender?.Stop();
            _unicastSender?.Dispose();
            Console.WriteLine("单播发送器已停止");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"停止单播发送器时出错: {ex.Message}");
        }

        try
        {
            _ptpClient?.Stop();
            Console.WriteLine("PTP客户端已停止");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"停止PTP客户端时出错: {ex.Message}");
        }
    }
}