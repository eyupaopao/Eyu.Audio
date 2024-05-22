using System.Runtime.InteropServices;
namespace Sample;

public class PulseAudioRecorder
{
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

    [StructLayout(LayoutKind.Sequential)]
    private struct pa_sample_spec
    {
        public int format;
        public int rate;
        public int channels;
    }

    private const int PA_STREAM_RECORD = 1;
    private const int PA_SAMPLE_S16LE = 3;

    public void RecordAudio(string outputFilePath, int durationInSeconds)
    {
        pa_sample_spec ss = new pa_sample_spec {
            format = PA_SAMPLE_S16LE,
            rate = 44100,
            channels = 2
        };

        int error;
        IntPtr pa = pa_simple_new(null, "CSharpRecorder", PA_STREAM_RECORD, null, "record", ref ss, IntPtr.Zero, IntPtr.Zero, out error);
        if (pa == IntPtr.Zero)
        {
            Console.WriteLine("Failed to connect to PulseAudio server.");
            return;
        }

        using (FileStream fs = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write))
        {
            byte[] buffer = new byte[4096];
            for (int i = 0; i < durationInSeconds * 10; i++) // 10 chunks per second
            {
                if (pa_simple_read(pa, buffer, buffer.Length, out error) < 0)
                {
                    Console.WriteLine("Failed to read data from PulseAudio.");
                    break;
                }
                fs.Write(buffer, 0, buffer.Length);
            }
        }

        pa_simple_free(pa);
    }


}