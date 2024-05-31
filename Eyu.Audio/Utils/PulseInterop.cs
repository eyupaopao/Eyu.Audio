using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace Eyu.Audio.Utils;

[SupportedOSPlatform("linux")]
internal class PulseInterop
{
    private const string PaLib = "libpulse-simple.so.0";
    [DllImport(PaLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int pa_simple_read(IntPtr s, byte[] data, int bytes, out int error);

    [DllImport(PaLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void pa_simple_free(IntPtr s);
    [DllImport(PaLib, CallingConvention = CallingConvention.Cdecl)]

    public static extern IntPtr pa_strerror(int error);
    [DllImport(PaLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr pa_mainloop_new();
    [DllImport(PaLib, CallingConvention = CallingConvention.Cdecl)]
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
    [StructLayout(LayoutKind.Sequential)]
    internal struct pa_sample_spec
    {
        public int format;
        public int rate;
        public int channels;
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
    static string RunCommand(string command)
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo {
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
            if (output.Contains("Command 'pactl' not found"))
                throw new Exception("no support please install pulseaudio");
            return output;
        }
        catch (Exception e)
        {
            return "Error executing command: " + e.Message;
        }
    }
    static string[] commmands = [
        "pactl unload-module module-echo-cancel",
        "pactl load-module module-echo-cancel aec_method=webrtc source_name=echocancel sink_name=echocancel1",
        "pacmd set-default-source echocancel",
        "pacmd set-default-sink echocancel1"];
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