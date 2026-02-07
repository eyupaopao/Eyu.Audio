using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;
using NAudio.Wave.Asio;
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
    public static DeviceEnumerator Instance => _instance ??= new DeviceEnumerator();

    private static DeviceEnumerator? _instance = null;

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
            ALSACaptureDevices = AlsaDeviceEnumerator.GetDefaultCaptureDevice();
            ALSARenderDevices = AlsaDeviceEnumerator.GetDefaultRenderDevice();
        }
        SdlApi.DeviceChangedAction += DeviceChanged;
        SdlCaptureDevices = SdlApi.GetDevices(1);
        SdlRenderDevices = SdlApi.GetDevices(0);
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
                Name = item.FriendlyName,
                DriverType = DriverType.Wasapi
            };
            WasapiRenderDevice.Add(device);
        }
        foreach (var item in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
        {
            var device = new AudioDevice
            {
                Id = item.ID,
                Device = item.FriendlyName,
                IsCapture = true,
                Name = item.FriendlyName,
                DriverType = DriverType.Wasapi
            };
            WasapiCaptureDevice.Add(device);
        }
        enumerator.RegisterEndpointNotificationCallback(this);
    }



    private void DeviceRemoved(string id)
    {
        var device = enumerator.GetDevice(id);
        if (device.DataFlow == DataFlow.Render)
        {
            var renderDevice = WasapiRenderDevice.FirstOrDefault(d => d.Id == id);
            if (renderDevice is not null)
            {
                WasapiRenderDevice.Remove(renderDevice);
                RenderDeviceChangedAction?.Invoke();
            }
        }
        else
        {
            var captureDevice = WasapiCaptureDevice.FirstOrDefault(d => d.Id == id);
            if (captureDevice is not null)
            {
                WasapiCaptureDevice.Remove(captureDevice);
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
            WasapiRenderDevice.Add(renderDevice);
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
            WasapiCaptureDevice.Add(captureDevice);
            CaptureDeviceChangedAction?.Invoke();
        }
    }

    #endregion

    #region sdl/linux

    private void DeviceChanged()
    {
        var capture = SdlApi.GetDevices(1);
        var except1 = SdlCaptureDevices.Except(capture).Any();
        var except2 = capture.Except(WasapiCaptureDevice).Any();
        if (except1 || except2)
        {
            SdlCaptureDevices = capture;
            CaptureDeviceChangedAction?.Invoke();
        }
        var render = SdlApi.GetDevices(0);
        var except3 = SdlRenderDevices.Except(render).Any();
        var except4 = render.Except(WasapiRenderDevice).Any();
        if (except3 || except4)
        {
            SdlRenderDevices = render;
            RenderDeviceChangedAction?.Invoke();
        }
    }
    #endregion

    #region create 
    public IWavePlayer? CreatePlayer(AudioDevice? audioDevice = null)
    {
        if (audioDevice == null)
        {
            return null;
        }
        else
        {
            if (audioDevice.DriverType == DriverType.Sdl)
                return new SDLOut(audioDevice);
            else if (audioDevice.DriverType == DriverType.Wasapi || RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var mmDevice = enumerator.GetDevice(audioDevice.Id);
                return new WasapiOut(mmDevice, AudioClientShareMode.Shared, useEventSync: true, 100);
            }
            else
                return new SDLOut(audioDevice);
        }
    }
    public IWaveIn CreateCapture(AudioDevice? audioDevice)
    {

        if (audioDevice.IsCapture)
        {
            switch (audioDevice?.DriverType)
            {
                case DriverType.Wasapi:
                    var mmDevice = enumerator.GetDevice(audioDevice.Id);
                    return new WasapiCapture(mmDevice);
                case DriverType.Sdl:
                    return new SDLCapture(audioDevice);
                default:
                    return new ALSACapture(audioDevice);
            }
        }
        else
        {
            return CreateLoopbackCapture(audioDevice);
        }
    }

    /// <summary>
    /// 创建环回采集（系统播放音频）。Windows 使用 WasapiLoopbackCapture，Linux 使用 PulseAudio/PipeWire monitor 源。
    /// </summary>
    public IWaveIn CreateLoopbackCapture(AudioDevice? audioDevice = default)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (audioDevice == null || audioDevice.DriverType != DriverType.Wasapi)
            {
                return new WasapiLoopbackCapture(WasapiLoopbackCapture.GetDefaultLoopbackCaptureDevice());
            }
            var mmDevice = enumerator.GetDevice(audioDevice.Id);
            return new WasapiLoopbackCapture(mmDevice);
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return new PulseLoopbackCapture();
        throw new PlatformNotSupportedException("当前平台不支持环回采集");
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
            catch (Exception ex)
            {
                throw new Exception($"Failed to initialize echo cancellation: {ex.Message}", ex);
            }
        }
        var device = WasapiCaptureDevice.FirstOrDefault(a => a.Device != null && a.Device.Contains("Echo-Cancel"))
            ?? throw new Exception("Not found echo cancel device");
        return new SDLCapture();
    }
    #endregion

    public Action? CaptureDeviceChangedAction;
    public Action? RenderDeviceChangedAction;
    public List<AudioDevice> WasapiRenderDevice = new();
    public List<AudioDevice> WasapiCaptureDevice = new();
    public List<AudioDevice> SdlCaptureDevices = new();
    public List<AudioDevice> SdlRenderDevices = new();
    public List<AudioDevice> ALSARenderDevices = new();
    public List<AudioDevice> ALSACaptureDevices = new();
}

public class AudioDevice
{
    public bool IsSelected { get; set; }
    public string? Id { get; set; }
    public int Index { get; set; }
    public bool IsCapture { get; set; }
    public string? Name { get; set; }
    public string? Device { get; set; }
    public DriverType DriverType { get; set; }
}
