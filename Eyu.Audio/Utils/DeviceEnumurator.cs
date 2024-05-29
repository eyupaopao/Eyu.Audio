using IPCASAPP.Cross.Utils;
using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Eyu.Audio.Utils;

public class DeviceEnumerator
{
    public static DeviceEnumerator Instance
    {
        get;
    } = new DeviceEnumerator();
    ~DeviceEnumerator()
    {
        enumerator.Dispose();
    }
    private DeviceEnumerator()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            WindowsDeviceMonitor();
        }
        else
        {
            LinuxDeviceMonitor();
        }
    }
    #region windows

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
        var mMNotificationClient = new NotificationClientImplementation();
        mMNotificationClient.OnDeviceAddedDelegate += DeviceAdded;
        mMNotificationClient.OnDeviceRemovedDelegate += DeviceRemoved;
        enumerator.RegisterEndpointNotificationCallback(mMNotificationClient);
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
    private void LinuxDeviceMonitor()
    {
        SdlApi.RenderDeviceChanged += this.RenderDeviceChanged;
        SdlApi.CaptureDeviceChanged += this.CaptureDeviceChanged;
    }

    private void CaptureDeviceChanged()
    {
        foreach (var item in SdlApi.InputDevices)
        {
            var device = new AudioDevice {
                Name = item.Name,
                Index = item.Index,
                IsCapture = item.Capture == 1,
            };
        }
        CaptureDeviceChangedAction?.Invoke();
    }

    private void RenderDeviceChanged()
    {
        foreach (var item in SdlApi.OutPutDevices)
        {
            var device = new AudioDevice {
                Name = item.Name,
                Index = item.Index,
                IsCapture = item.Capture == 1,
            };
        }
        RenderDeviceChangedAction?.Invoke();
    }

    public Action CaptureDeviceChangedAction;
    public Action RenderDeviceChangedAction;
    public List<AudioDevice> RenderDevice = new();
    public List<AudioDevice> CaptureDevice = new();
}

public class AudioDevice
{
    public string? Id;
    public int Index;
    public bool IsCapture;
    public string? Name;
}
