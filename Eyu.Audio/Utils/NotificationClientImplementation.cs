using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System;

namespace IPCASAPP.Cross.Utils;

/// <summary>
/// 音频设备获取
/// </summary>
//internal class NotificationClientImplementation : IMMNotificationClient
//{
//    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
//    {
//        OnDefaultDeviceChangedDelegate?.Invoke(flow,role,defaultDeviceId);
//    }

//    public void OnDeviceAdded(string pwstrDeviceId)
//    {
//        OnDeviceAddedDelegate?.Invoke(pwstrDeviceId);
//    }

//    public void OnDeviceRemoved(string deviceId)
//    {
//        OnDeviceRemovedDelegate?.Invoke(deviceId);
//    }

//    public void OnDeviceStateChanged(string deviceId, DeviceState newState)
//    {
//        OnDeviceStateChangedDelegate?.Invoke(deviceId, newState);
//    }

//    public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
//    {
//        OnPropertyValueChangedDelegate?.Invoke(pwstrDeviceId, key);
//    }

//    public Action<DataFlow, Role, string> OnDefaultDeviceChangedDelegate;
//    public Action<string> OnDeviceAddedDelegate;
//    public Action<string> OnDeviceRemovedDelegate;
//    public Action<string, DeviceState> OnDeviceStateChangedDelegate;
//    public Action<string, PropertyKey> OnPropertyValueChangedDelegate;
//}