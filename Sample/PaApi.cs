using System.Runtime.InteropServices;

namespace Eyu.Audio;

internal unsafe class PaApi
{
    private const string PaLib = "libpulse-simple.so.0";
    [DllImport(PaLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int pa_simple_read(IntPtr s, byte[] data, int bytes, out int error);

    [DllImport(PaLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void pa_simple_free(IntPtr s);
    [DllImport(PaLib, CallingConvention = CallingConvention.Cdecl)]

    public static extern IntPtr pa_strerror(int error);
    [DllImport(PaLib)]
    public static extern IntPtr pa_mainloop_new();

}