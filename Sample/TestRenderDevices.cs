using Eyu.Audio.Utils;
using System;
using System.Collections.Generic;

namespace Sample
{
    class TestRenderDevices
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Testing AlsaDeviceEnumerator.GetRenderDevices() method:");
            Console.WriteLine();

            try
            {
                // Test getting render devices
                List<AudioDevice> renderDevices = AlsaDeviceEnumerator.GetRenderDevices();
                
                Console.WriteLine($"Found {renderDevices.Count} render device(s):");
                foreach (var device in renderDevices)
                {
                    Console.WriteLine($"  Device: {device.Device}");
                    Console.WriteLine($"  Name: {device.Name}");
                    Console.WriteLine($"  IsCapture: {device.IsCapture}");
                    Console.WriteLine();
                }
                
                if (renderDevices.Count == 0)
                {
                    Console.WriteLine("No render devices found. This might be because:");
                    Console.WriteLine("- No audio devices are connected");
                    Console.WriteLine("- ALSA utilities (aplay) are not installed");
                    Console.WriteLine("- The program doesn't have permission to access audio devices");
                    Console.WriteLine();
                    Console.WriteLine("Trying fallback device:");
                    var fallback = new AudioDevice 
                    { 
                        Device = "default", 
                        Name = "Default ALSA Device",
                        IsCapture = false
                    };
                    Console.WriteLine($"  Device: {fallback.Device}");
                    Console.WriteLine($"  Name: {fallback.Name}");
                    Console.WriteLine($"  IsCapture: {fallback.IsCapture}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred while enumerating render devices: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            
            Console.WriteLine();
            Console.WriteLine("Testing completed.");
        }
    }
}