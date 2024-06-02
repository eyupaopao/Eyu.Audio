using Eyu.Audio.Recorder;
using IPCASAPP.Cross.Utils;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Eyu.Audio.Utils;

public class DeviceEnumerator : IMMNotificationClient
{
    public static DeviceEnumerator Instance
    {
        get; set;
    }
    public bool UseSdl
    {
        get;
    }

    public static void CreateInstance(bool useSdl = false)
    {
        if (Instance == null)
        {
            Instance = new DeviceEnumerator(useSdl);
        }
    }
    ~DeviceEnumerator()
    {
        enumerator.Dispose();
    }

    private DeviceEnumerator(bool useSdl)
    {
        if (useSdl)
        {
            SdlDeviceMonitor();
            return;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            WindowsDeviceMonitor();
            return;
        }

        SdlDeviceMonitor();
        UseSdl = useSdl;
    }
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
            var device = new AudioDevice {
                Id = item.ID,
                Name = item.FriendlyName,
                IsCapture = false
            };
            RenderDevice.Add(device);
        }
        foreach (var item in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
        {
            var device = new AudioDevice {

                Id = item.ID,
                Name = item.FriendlyName,
                IsCapture = true
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
            var renderDevice = new AudioDevice {

                Id = device.ID,
                Name = device.FriendlyName,
                IsCapture = false
            };
            RenderDevice.Add(renderDevice);
            RenderDeviceChangedAction?.Invoke();
        }
        else
        {
            var captureDevice = new AudioDevice {

                Id = device.ID,
                Name = device.FriendlyName,
                IsCapture = true
            };
            CaptureDevice.Add(captureDevice);
            CaptureDeviceChangedAction?.Invoke();
        }
    }

    #endregion
    private void SdlDeviceMonitor()
    {
        CaptureDevice = SdlApi.GetDevices(1);
        RenderDevice = SdlApi.GetDevices(0);
        SdlApi.DeviceChangedAction += DeviceChanged;
    }

    private void DeviceChanged()
    {
        var capture = SdlApi.GetDevices(1);
        var except1 = CaptureDevice.Except(capture).Any();
        var except2 = capture.Except(CaptureDevice).Any();
        if (except1 || except2)
        {
            CaptureDevice = capture;
            CaptureDeviceChangedAction?.Invoke();
        }
        var render = SdlApi.GetDevices(0);
        var except3 = RenderDevice.Except(render).Any();
        var except4 = render.Except(RenderDevice).Any();
        if (except3 || except4)
        {
            RenderDevice = render;
            RenderDeviceChangedAction?.Invoke();
        }
    }

    public IWavePlayer CreatePlayer(AudioDevice audioDevice = null)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || UseSdl)
        {
            return new SDLOut(audioDevice);
        }
        if (audioDevice != null)
        {
            var mmDevice = enumerator.GetDevice(audioDevice.Id);
            return new WasapiOut(mmDevice, AudioClientShareMode.Shared, useEventSync: true, 200);
        }
        else
        {
            return new WasapiOut();
        }
    }
    public IWaveIn CreateCapture(AudioDevice audioDevice = null)
    {
        if (UseSdl || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (audioDevice?.IsCapture == false)
                throw new Exception("Not support os platform");
            return new SDLCapture(audioDevice);
        }
        if (audioDevice == null)
            return new WasapiCapture();
        else
        {
            var mmDevice = enumerator.GetDevice(audioDevice.Id);
            return new WasapiCapture(mmDevice);
        }
    }
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
        var device = CaptureDevice.FirstOrDefault(a => a.Name.Contains("Echo-Cancel"));
        if (device == null)
        {
            throw new Exception("Not found echo cancel device");
        }
        return new SDLCapture();
    }

    public Action CaptureDeviceChangedAction;
    public Action RenderDeviceChangedAction;
    public List<AudioDevice> RenderDevice = new();
    public List<AudioDevice> CaptureDevice = new();
}

public class AudioDevice
{
    public bool IsSelected { get; set; }
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
}
