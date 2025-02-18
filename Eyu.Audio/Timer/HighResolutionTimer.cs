using System;
using System.Runtime.InteropServices;

namespace Eyu.Audio.Timer;
/// <summary>
/// High performance (precision) timer
/// </summary>
public class HighResolutionTimer : ITimer, IDisposable
{
    private readonly ITimer timer;

    public HighResolutionTimer(Action tick)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            timer = new MultimediaTimer(tick);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            if (nint.Size == 8)
                timer = new LinuxTimer64(tick);
            else
                timer = new LinuxTimer(tick);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {

        }
        else
        {
            throw new NotSupportedException(); 
        }
    }

    /// <summary>
    /// Dispose all resources
    /// </summary>
    public void Dispose()
    {
        timer.Dispose();
    }

    /// <summary>
    /// Set the frequency of the timer in milliseconds. For example 25 ms would generate a 40 Hz timer (1000/25=40)
    /// </summary>
    /// <param name="periodMS">Period in MS</param>
    public void SetPeriod(int periodMS)
    {
        timer.SetPeriod(periodMS);
    }

    public void Start()
    {
        timer.Start();
    }

    public void Stop()
    {
        timer.Stop();
    }

}

