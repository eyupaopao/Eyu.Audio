using Silk.NET.SDL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eyu.Audio.Utils;

public unsafe static class SdlApi
{
    static Sdl api = Sdl.GetApi();
    static unsafe SdlApi()
    {
        api.Init(Sdl.InitAudio | Sdl.InitEvents);
        api.SetEventFilter(new(OnDeviceChange), null);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }


    public static event EventHandler<IEnumerable<SDLDevice>> CaptureDeviceChanged;
    public static event EventHandler<IEnumerable<SDLDevice>> RenderDeviceChanged;

    static unsafe int OnDeviceChange(void* sender, Event* e)
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
        return 0;
    }

    static unsafe List<SDLDevice> GetDevices(int capture)
    {
        var list = new List<SDLDevice>();
        var num = api.GetNumAudioDevices(capture);
        if (num > 1)
        {
            for (int i = 0; i < num; i++)
            {
                var name = Encoding.UTF8.GetString(Encoding.GetEncoding("GBK").GetBytes(api.GetAudioDeviceNameS(i, capture)));
                //IntPtr namePtr = new IntPtr(_sdl.GetAudioDeviceName(i, capture));

                list.Add(new SDLDevice(name, i, capture));
            }
        }
        return list;
    }

    internal static void CloseAudioDevice(uint device)
    {
        api.CloseAudioDevice(device);
    }

    internal static void PauseAudioDevice(uint device, int pause_on)
    {
        api.PauseAudioDevice(device, pause_on);
    }

    internal static int InitAudio()
    {
        return api.InitSubSystem(Sdl.InitAudio);
    }

    internal static Exception? GetErrorAsException()
    {
        return api.GetErrorAsException();
    }

    internal static byte* GetAudioDeviceName(int deviceIndex, int isCapture)
    {
        return api.GetAudioDeviceName(deviceIndex, isCapture);
    }

    internal static uint OpenAudioDevice(byte* deviceName, int isCapture, AudioSpec* sourceSpec, AudioSpec* targetSpec)
    {
        return api.OpenAudioDevice(deviceName, isCapture, sourceSpec, targetSpec, (int)Sdl.AudioAllowAnyChange);
    }

}
