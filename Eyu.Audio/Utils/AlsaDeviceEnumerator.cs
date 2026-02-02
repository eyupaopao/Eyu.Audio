using Eyu.Audio.Alsa;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Eyu.Audio.Utils
{
    /// <summary>
    /// Provides functionality to enumerate ALSA devices programmatically
    /// </summary>
    public static class AlsaDeviceEnumerator
    {
        /// <summary>
        /// Enumerates capture devices using ALSA native API
        /// </summary>
        /// <returns>List of available capture devices</returns>
        public static List<AudioDevice> GetCaptureDevices()
        {
            var devices = new List<AudioDevice>();
            
            // For now, we'll use the system command approach as the native ALSA 
            // enumeration functions need to be properly implemented
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "bash",
                    Arguments = "-c \"arecord -L | grep -E '^[[:space:]]*[^[:space:]]'\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = System.Diagnostics.Process.Start(startInfo))
                {
                    string output = process?.StandardOutput.ReadToEnd();
                    
                    process?.WaitForExit();
                    
                    if (!string.IsNullOrEmpty(output))
                    {
                        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                        {
                            var trimmedLine = line.Trim();
                            if (!string.IsNullOrEmpty(trimmedLine) && !trimmedLine.StartsWith("null"))
                            {
                                // Extract device name (first part before space)
                                var parts = trimmedLine.Split(new[] { ' ', '\t' }, 2);
                                if (parts.Length > 0)
                                {
                                    var deviceName = parts[0];
                                    var deviceDescription = parts.Length > 1 ? parts[1] : deviceName;
                                    
                                    devices.Add(new AudioDevice 
                                    { 
                                        Device = deviceName, 
                                        Name = deviceDescription,
                                        IsCapture = true
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Fallback to default device if enumeration fails
                devices.Add(new AudioDevice 
                { 
                    Device = "default", 
                    Name = "Default ALSA Device",
                    IsCapture = true
                });
            }
            
            return devices;
        }
        
        /// <summary>
        /// Enumerates playback devices using ALSA native API
        /// </summary>
        /// <returns>List of available playback devices</returns>
        public static List<AudioDevice> GetPlaybackDevices()
        {
            var devices = new List<AudioDevice>();
            
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "bash",
                    Arguments = "-c \"aplay -L | grep -E '^[[:space:]]*[^[:space:]]'\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = System.Diagnostics.Process.Start(startInfo))
                {
                    string output = process?.StandardOutput.ReadToEnd();
                    
                    process?.WaitForExit();
                    
                    if (!string.IsNullOrEmpty(output))
                    {
                        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                        {
                            var trimmedLine = line.Trim();
                            if (!string.IsNullOrEmpty(trimmedLine) && !trimmedLine.StartsWith("null"))
                            {
                                // Extract device name (first part before space)
                                var parts = trimmedLine.Split(new[] { ' ', '\t' }, 2);
                                if (parts.Length > 0)
                                {
                                    var deviceName = parts[0];
                                    var deviceDescription = parts.Length > 1 ? parts[1] : deviceName;
                                    
                                    devices.Add(new AudioDevice 
                                    { 
                                        Device = deviceName, 
                                        Name = deviceDescription,
                                        IsCapture = false
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Fallback to default device if enumeration fails
                devices.Add(new AudioDevice 
                { 
                    Device = "default", 
                    Name = "Default ALSA Device",
                    IsCapture = false
                });
            }
            
            return devices;
        }
        
        /// <summary>
        /// Enumerates render devices using ALSA native API (alias for GetPlaybackDevices)
        /// </summary>
        /// <returns>List of available render devices</returns>
        public static List<AudioDevice> GetRenderDevices()
        {
            var devices = new List<AudioDevice>();
            
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "bash",
                    Arguments = "-c \"aplay -L | grep -E '^[[:space:]]*[^[:space:]]'\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = System.Diagnostics.Process.Start(startInfo))
                {
                    string output = process?.StandardOutput.ReadToEnd();
                    
                    process?.WaitForExit();
                    
                    if (!string.IsNullOrEmpty(output))
                    {
                        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                        {
                            var trimmedLine = line.Trim();
                            if (!string.IsNullOrEmpty(trimmedLine) && !trimmedLine.StartsWith("null"))
                            {
                                // Extract device name (first part before space)
                                var parts = trimmedLine.Split(new[] { ' ', '\t' }, 2);
                                if (parts.Length > 0)
                                {
                                    var deviceName = parts[0];
                                    var deviceDescription = parts.Length > 1 ? parts[1] : deviceName;
                                    
                                    devices.Add(new AudioDevice 
                                    { 
                                        Device = deviceName, 
                                        Name = deviceDescription,
                                        IsCapture = false
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Fallback to default device if enumeration fails
                devices.Add(new AudioDevice 
                { 
                    Device = "default", 
                    Name = "Default ALSA Device",
                    IsCapture = false
                });
            }
            
            return devices;
        }
        
        /// <summary>
        /// Gets only the default capture device using ALSA
        /// </summary>
        /// <returns>The default capture device</returns>
        public static List<AudioDevice> GetDefaultCaptureDevice()
        {
            var allDevices = GetCaptureDevices();
            var defaultDevice = allDevices.FirstOrDefault(d => d.Device == "default") ?? 
                              allDevices.FirstOrDefault() ?? 
                              new AudioDevice 
                              { 
                                  Device = "default", 
                                  Name = "Default ALSA Device",
                                  IsCapture = true
                              };
            return new List<AudioDevice> { defaultDevice };
        }
        
        /// <summary>
        /// Gets only the default render device using ALSA
        /// </summary>
        /// <returns>The default render device</returns>
        public static List<AudioDevice> GetDefaultRenderDevice()
        {
            var allDevices = GetRenderDevices();
            var defaultDevice = allDevices.FirstOrDefault(d => d.Device == "default") ?? 
                              allDevices.FirstOrDefault() ?? 
                              new AudioDevice 
                              { 
                                  Device = "default", 
                                  Name = "Default ALSA Device",
                                  IsCapture = false
                              };
            return new List<AudioDevice> { defaultDevice };
        }
        
        /// <summary>
        /// Checks if a specific device exists and is accessible
        /// </summary>
        /// <param name="deviceName">Name of the device to check</param>
        /// <param name="isCapture">Whether to check for capture (true) or playback (false) device</param>
        /// <returns>True if the device exists and is accessible</returns>
        public static bool DeviceExists(string deviceName, bool isCapture = true)
        {
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "bash",
                    Arguments = isCapture ? 
                        $"-c \"arecord -L | grep -q '^\\\\s*{deviceName}'\"" :
                        $"-c \"aplay -L | grep -q '^\\\\s*{deviceName}'\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = System.Diagnostics.Process.Start(startInfo))
                {
                    process?.WaitForExit();
                    return process?.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
