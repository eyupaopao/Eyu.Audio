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

    static SdlApi()
    {
        var res = Api.Init(Sdl.InitAudio | Sdl.InitEvents);
        if (res != 0)
        {
            throw Api.GetErrorAsException();
        }

        Api.SetEventFilter(new(OnDeviceChange), null);
    }


    public static Action DeviceChangedAction;

    static int OnDeviceChange(void* sender, Event* e)
    {
        var type = e->Type;
        Task.Run(() =>
        {
            try
            {
                if (type is (int)EventType.Audiodeviceadded or (int)EventType.Audiodeviceremoved)
                {
                    DeviceChangedAction?.Invoke();
                }
            }
            catch (Exception ex)
            {

            }
        });
        return 0;
    }

    public static List<AudioDevice> GetDevices(int capture)
    {
        var list = new List<AudioDevice>();
        var num = Api.GetNumAudioDevices(capture);
        if (num > 0)
        {
            for (int i = 0; i < num; i++)
            {
                var ptr = Api.GetAudioDeviceName(i, capture);
                var dev = Api.GetAudioDriver(i);
                var name = Marshal.PtrToStringUTF8(new IntPtr(ptr));
                list.Add(new AudioDevice 
                { 
                    Device = name, 
                    Index = i, 
                    IsCapture = capture == 1 ,
                    Name = name
                });
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
