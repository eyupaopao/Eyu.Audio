using NAudio.Wave;
using Silk.NET.SDL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Eyu.Audio.Utils;

public unsafe static class SdlApi
{
    public static Sdl Api = Sdl.GetApi();
    static unsafe SdlApi()
    {
        var res = Api.Init(Sdl.InitAudio | Sdl.InitEvents);
        if (res != 0)
        {
            throw Api.GetErrorAsException();
        }
        Api.SetEventFilter(new(OnDeviceChange), null);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }


    public static event EventHandler<IEnumerable<SDLDevice>>? CaptureDeviceChanged;
    public static event EventHandler<IEnumerable<SDLDevice>>? RenderDeviceChanged;

    static unsafe int OnDeviceChange(void* sender, Event* e)
    {
        Task.Run(() =>
        {
            try
            {
                if (e->Type is (int)EventType.Audiodeviceadded or (int)EventType.Audiodeviceremoved)
                {
                    if (CaptureDeviceChanged != null)
                    {
                        CaptureDeviceChanged?.Invoke(null, GetDevices(1));
                    }
                    if (RenderDeviceChanged != null)
                    {
                        RenderDeviceChanged?.Invoke(null, GetDevices(0));

                    }
                }
            }
            catch (Exception ex)
            {

            }
        });
        return 0;
    }

    public static unsafe List<SDLDevice> GetDevices(int capture)
    {
        var list = new List<SDLDevice>();
        var num = Api.GetNumAudioDevices(capture);
        if (num > 0)
        {
            for (int i = 0; i < num; i++)
            {
                var ptr = Api.GetAudioDeviceName(i, capture);
                var name = Marshal.PtrToStringUTF8(new IntPtr(ptr));
                list.Add(new SDLDevice(name, i, capture));
            }
        }
        return list;
    }
    #region errors

    public static string NoOutputDevice = "No output device";
    public static string NoInputDevice = "No input device";
    public static string ErrorDeviceTyep = "Error device type";
    #endregion
}
