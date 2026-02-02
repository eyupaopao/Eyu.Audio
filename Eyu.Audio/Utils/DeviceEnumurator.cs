using Eyu.Audio.Recorder;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Eyu.Audio.Utils;

public enum DriverType
{
    Wasapi,
    Sdl,
    Alsa,
}
public class DeviceEnumerator : IMMNotificationClient
{
    public static DeviceEnumerator Instance = null!;
   

    public static void CreateInstance(DriverType captureType, DriverType renderType)
    {
        if (Instance == null)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if(captureType == DriverType.Alsa) captureType = DriverType.Wasapi;
                if (renderType == DriverType.Alsa) renderType = DriverType.Wasapi;
                Instance = new DeviceEnumerator(captureType, renderType);
                return;
            }
            else
            {
                Instance = new DeviceEnumerator(DriverType.Alsa, DriverType.Sdl);
                return;
            }
        }
    }
    ~DeviceEnumerator()
    {
        enumerator.Dispose();
    }

    private DeviceEnumerator(DriverType captureType, DriverType renderType)
    {

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            captureType = DriverType.Wasapi;
            renderType = DriverType.Wasapi;
            WindowsDeviceMonitor();
            return;
        }
        else
        {
            if (captureType == DriverType.Sdl)
                CaptureDevice = SdlApi.GetDevices(1);
            else if (captureType == DriverType.Alsa)
                CaptureDevice = AlsaDeviceEnumerator.GetDefaultCaptureDevice();
            if (renderType == DriverType.Sdl)
                CaptureDevice = SdlApi.GetDevices(1);
            else if (renderType == DriverType.Alsa)
                RenderDevice = AlsaDeviceEnumerator.GetDefaultRenderDevice();
            if (captureType == DriverType.Sdl || renderType == DriverType.Sdl)
                SdlApi.DeviceChangedAction += DeviceChanged;
        }

        //SdlDeviceMonitor();
    }
    public DriverType captureType = DriverType.Wasapi;
    public DriverType renderType = DriverType.Wasapi;
    #region windows
    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
        //OnDefaultDeviceChangedDelegate?.Invoke(flow, role, defaultDeviceId);
    }

    public void OnDeviceAdded(string pwstrDeviceId)
    {
        //OnDeviceAddedDelegate?.Invoke(pwstrDeviceId);
    }

    public void OnDeviceRemoved(string deviceId)
    {
        //OnDeviceRemovedDelegate?.Invoke(deviceId);
    }

    public void OnDeviceStateChanged(string deviceId, DeviceState newState)
    {
        if (newState == DeviceState.Active)
        {
            DeviceAdded(deviceId);
        }
        else
        {
            DeviceRemoved(deviceId);
        }
        //OnDeviceStateChangedDelegate?.Invoke(deviceId, newState);
    }

    public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
    {
        //OnPropertyValueChangedDelegate?.Invoke(pwstrDeviceId, key);
    }

    //public Action<DataFlow, Role, string> OnDefaultDeviceChangedDelegate;
    //public Action<string> OnDeviceAddedDelegate;
    //public Action<string> OnDeviceRemovedDelegate;
    //public Action<string, DeviceState> OnDeviceStateChangedDelegate;
    //public Action<string, PropertyKey> OnPropertyValueChangedDelegate;
    private MMDeviceEnumerator enumerator = null!;
    private void WindowsDeviceMonitor()
    {
        enumerator = new MMDeviceEnumerator();
        foreach (var item in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            var device = new AudioDevice
            {
                Id = item.ID,
                Device = item.FriendlyName,
                IsCapture = false,
                Name = item.FriendlyName
            };
            RenderDevice.Add(device);
        }
        foreach (var item in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
        {
            var device = new AudioDevice
            {
                Id = item.ID,
                Device = item.FriendlyName,
                IsCapture = true,
                Name = item.FriendlyName
            };
            CaptureDevice.Add(device);
        }
        enumerator.RegisterEndpointNotificationCallback(this);
    }



    private void DeviceRemoved(string id)
    {
        var device = enumerator.GetDevice(id);
        if (device.DataFlow == DataFlow.Render)
        {
            var renderDevice = RenderDevice.FirstOrDefault(d => d.Id == id);
            if (renderDevice is not null)
            {
                RenderDevice.Remove(renderDevice);
                RenderDeviceChangedAction?.Invoke();
            }
        }
        else
        {
            var captureDevice = CaptureDevice.FirstOrDefault(d => d.Id == id);
            if (captureDevice is not null)
            {
                CaptureDevice.Remove(captureDevice);
                CaptureDeviceChangedAction?.Invoke();
            }
        }
    }

    private void DeviceAdded(string id)
    {
        var device = enumerator.GetDevice(id);
        if (device.DataFlow == DataFlow.Render)
        {
            var renderDevice = new AudioDevice
            {

                Id = device.ID,
                Device = device.FriendlyName,
                IsCapture = false,
                Name = device.FriendlyName
            };
            RenderDevice.Add(renderDevice);
            RenderDeviceChangedAction?.Invoke();
        }
        else
        {
            var captureDevice = new AudioDevice
            {

                Id = device.ID,
                Device = device.FriendlyName,
                IsCapture = true,
                Name = device.FriendlyName
            };
            CaptureDevice.Add(captureDevice);
            CaptureDeviceChangedAction?.Invoke();
        }
    }

    #endregion

    #region sdl/linux
    
    private void DeviceChanged()
    {
        if (captureType == DriverType.Sdl)
        {
            var capture = SdlApi.GetDevices(1);
            var except1 = CaptureDevice.Except(capture).Any();
            var except2 = capture.Except(CaptureDevice).Any();
            if (except1 || except2)
            {
                CaptureDevice = capture;
                CaptureDeviceChangedAction?.Invoke();
            }
        }
        if (captureType == DriverType.Sdl)
        {
            var render = SdlApi.GetDevices(0);
            var except3 = RenderDevice.Except(render).Any();
            var except4 = render.Except(RenderDevice).Any();
            if (except3 || except4)
            {
                RenderDevice = render;
                RenderDeviceChangedAction?.Invoke();
            }
        }
    }
    #endregion

    public IWavePlayer CreatePlayer(AudioDevice? audioDevice = null)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || renderType == DriverType.Sdl)
        {
            return new SDLOut(audioDevice);
        }
        if (audioDevice != null)
        {
            var mmDevice = enumerator.GetDevice(audioDevice.Id);
            return new WasapiOut(mmDevice, AudioClientShareMode.Shared, useEventSync: true, 100);
        }
        else
        {
            return new WasapiOut();
        }
    }
    public IWaveIn CreateCapture(AudioDevice? audioDevice = null)
    {
        if (captureType == DriverType.Sdl || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (audioDevice?.IsCapture == false)
                throw new Exception("Not support os platform");
            //return new SDLCapture(audioDevice);
            return new AlsaCapture(audioDevice);
        }
        if (audioDevice == null)
            return new WasapiCapture();
        else
        {
            var mmDevice = enumerator.GetDevice(audioDevice.Id);
            if (mmDevice.DataFlow == DataFlow.Render)
                return new WasapiLoopbackCapture(mmDevice);
            return new WasapiCapture(mmDevice);
        }
    }
    [SupportedOSPlatform("Linux")]
    public IWaveIn CreateCaptureEchoCancel()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new Exception("Not support os platform");
        }
        else
        {
            try
            {
                PulseInterop.OpenCancel();
            }
            catch
            {
                throw;
            }
        }
        var device = CaptureDevice.FirstOrDefault(a => a.Device != null && a.Device.Contains("Echo-Cancel"))
            ?? throw new Exception("Not found echo cancel device");
        return new SDLCapture();
    }

    public Action? CaptureDeviceChangedAction;
    public Action? RenderDeviceChangedAction;
    public List<AudioDevice> RenderDevice = new();
    public List<AudioDevice> CaptureDevice = new();
}

public class AudioDevice
{
    public bool IsSelected
    {
        get; set;
    }
    public string? Id
    {
        get; set;
    }
    public int Index
    {
        get; set;
    }
    public bool IsCapture
    {
        get; set;
    }
    public string? Name
    {
        get; set;
    }
    public string? Device
    {
        get; set;
    }
}
