using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.Wave;
namespace Eyu.Audio;
/// <summary>
/// Contains classes related to audio capturing and recording using PulseAudio.
/// </summary>
public class PulseCapture : IWaveIn
{
    public PulseCapture(AudioDevice audioDevice)
    {
        WaveFormat = new WaveFormat(48000, 16, 2);
        this.audioDevice = audioDevice;
        sourceWaveFormat = audioDevice.WaveFormat;
    }
    [DllImport("libpulse-simple.so.0")]
    private static extern IntPtr pa_simple_new(
        string server,
        string name,
        int dir,
        string dev,
        string stream_name,
        ref pa_sample_spec ss,
        IntPtr channel_map,
        IntPtr attr,
        out int error);

    [DllImport("libpulse-simple.so.0")]
    private static extern int pa_simple_read(IntPtr s, byte[] data, int bytes, out int error);

    [DllImport("libpulse-simple.so.0")]
    private static extern void pa_simple_free(IntPtr s);
    [DllImport("libpulse-simple.so.0", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr pa_strerror(int error);
    private const int PA_STREAM_RECORD = 1;
    private readonly AudioDevice audioDevice;
    private WaveFormat sourceWaveFormat;
    private bool isRecording;
    public event EventHandler<WaveInEventArgs> DataAvailable;
    public event EventHandler<StoppedEventArgs> RecordingStopped;

    public WaveFormat WaveFormat { get; set; }


    public static List<AudioDevice> GetDevices()
    {
        var devices = new List<AudioDevice>();
        string recordDevices = RunCommand("pactl list sources short");
        Console.WriteLine("设备列表:\n");
        var lines = recordDevices.Split('\n');
        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line)) continue;
            var deviceInfo = line.Split('\t');
            if (deviceInfo.Length < 3) continue;
            if (deviceInfo[0] == "0") continue;
            var formatInfo = deviceInfo[3].Split(' ');
            var sampleRate = int.Parse(formatInfo[2].Trim("Hz".ToCharArray()));
            var channels = int.Parse(formatInfo[1].Trim("ch".ToCharArray()));
            var WaveFormat = new WaveFormat();
            if (formatInfo[0].Contains("float32"))
            {
                WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
            }
            else if (formatInfo[0].Contains("s16le"))
            {
                WaveFormat = new WaveFormat(sampleRate, 16, channels);
            }
            else if (formatInfo[0].Contains("s24le"))
            {
                WaveFormat = new WaveFormat(sampleRate, 24, channels);
            }
            else if (formatInfo[0].Contains("s32le"))
            {
                WaveFormat = new WaveFormat(sampleRate, 32, channels);
            }
            var device = new AudioDevice
            {
                Id = deviceInfo[0],
                Name = deviceInfo[1],
                WaveFormat = WaveFormat,
            };
            if (device.Name.Contains("input"))
            {
                device.IsCapture = true;
            }
            devices.Add(device);
        }
        return devices;
    }

    public static void OpenCancel()
    {
        foreach (var cmd in commmands)
        {
            var output = RunCommand(cmd);
        }
    }
    public static void CloseCancel()
    {
        var output = RunCommand(commmands[0]);
    }
    static string[] commmands = [
        "pactl unload-module module-echo-cancel",
        "pactl load-module module-echo-cancel aec_method=webrtc source_name=echocancel sink_name=echocancel1",
        "pacmd set-default-source echocancel",
        "pacmd set-default-sink echocancel1"];
    static string RunCommand(string command)
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
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

    public void StartRecording()
    {
        var format = PaSampleFormat.PA_SAMPLE_U8;
        if (WaveFormat.BitsPerSample == 16)
        {
            format = PaSampleFormat.PA_SAMPLE_S16LE;
        }
        else if (WaveFormat.BitsPerSample == 32)
        {
            if (WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
            {
                format = PaSampleFormat.PA_SAMPLE_FLOAT32LE;
            }
            else
            {
                format = PaSampleFormat.PA_SAMPLE_S32LE;
            }
        }
        else if (WaveFormat.BitsPerSample == 24)
        {
            format = PaSampleFormat.PA_SAMPLE_S24LE;
        }
        pa_sample_spec ss = new pa_sample_spec
        {
            format = (int)format,
            rate = WaveFormat.SampleRate,
            channels = WaveFormat.Channels,
        };

        int error;
        pa = pa_simple_new(null, "CSharpRecorder", PA_STREAM_RECORD, audioDevice.Name, "record", ref ss, IntPtr.Zero, IntPtr.Zero, out error);
        if (pa == IntPtr.Zero)
        {
            RecordingStopped?.Invoke(this, new StoppedEventArgs(new Exception("Failed to open PulseAudio stream." + Marshal.PtrToStringAnsi(pa_strerror(error)))));
            return;
        }


        isRecording = true;
        byte[] buffer = new byte[4096];
        Task.Run(async () =>
        {

            while (isRecording)
            {
                if (pa_simple_read(pa, buffer, buffer.Length, out error) < 0)
                {
                    Console.WriteLine("pa_simple_read failed: " + Marshal.PtrToStringAnsi(pa_strerror(error)));
                    continue;
                }
                DataAvailable?.Invoke(this, new WaveInEventArgs(buffer, buffer.Length));
                await Task.Delay(1);
            }
        });
    }

    IntPtr pa = IntPtr.Zero;
    public void StopRecording()
    {
        if (pa != IntPtr.Zero)
        {
            pa_simple_free(pa);
            pa = IntPtr.Zero;
        }
        RecordingStopped?.Invoke(this, new StoppedEventArgs(null));
        isRecording = false;
    }

    public void Dispose()
    {
        StopRecording();
    }
}


[StructLayout(LayoutKind.Sequential)]
internal struct pa_sample_spec
{
    public int format;
    public int rate;
    public int channels;
}
internal enum PaSampleFormat
{
    PA_SAMPLE_U8,
    PA_SAMPLE_ALAW,
    PA_SAMPLE_ULAW,
    PA_SAMPLE_S16LE,
    PA_SAMPLE_S16BE,
    PA_SAMPLE_FLOAT32LE,
    PA_SAMPLE_FLOAT32BE,
    PA_SAMPLE_S32LE,
    PA_SAMPLE_S32BE,
    PA_SAMPLE_S24LE,
    PA_SAMPLE_S24BE,
    PA_SAMPLE_S24_32LE,
    PA_SAMPLE_S24_32BE,

    PA_SAMPLE_MAX,
    PA_SAMPLE_INVALID = -1
}

public class AudioDevice
{
    public string Id
    {
        get; set;
    }
    public string Name
    {
        get; set;
    }
    public WaveFormat WaveFormat { get; set; }
    public bool IsCapture
    {
        get; set;
    }
}