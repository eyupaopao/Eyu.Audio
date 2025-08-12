namespace Eyu.Audio.Alsa;

/// <summary>
/// connect your sound device configuration to a virtual interface
/// </summary>
public static class AlsaDeviceBuilder
{
    /// <summary>
    /// create and connect a sound device.
    /// use <see cref="SoundDeviceSettings"/> for parameter set up.
    /// </summary>
    /// <param Name="settings">sound device configuration to use</param>
    /// <returns>sound device ready to use</returns>
    public static ISoundDevice Create(SoundDeviceSettings settings) => new UnixSoundDevice(settings);
}
