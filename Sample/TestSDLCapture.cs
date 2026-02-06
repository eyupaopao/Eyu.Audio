using Eyu.Audio;
using Eyu.Audio.Utils;
using NAudio.Wave;
using System;
using System.IO;
using System.Diagnostics;

namespace Sample
{
    public class TestSDLCapture
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Testing SDLCapture start/stop cycle 6 times...");
            
            try
            {
                // Initialize the device enumerator first with SDL for capture
                DeviceEnumerator.CreateInstance(DriverType.Sdl, DriverType.Sdl);
                var enumerator = DeviceEnumerator.Instance;
                // Get available capture devices
                var captureDevices = enumerator.CaptureDevice;
                Console.WriteLine($"Found {captureDevices.Count} capture devices:");
                
                foreach (var device in captureDevices)
                {
                    Console.WriteLine($" - {device.Device}: {device.Name}");
                }
                
                // Use the first available capture device, or default if none found
                Eyu.Audio.Utils.AudioDevice? audioDevice = captureDevices.Count > 0 ? captureDevices[0] : null;
                
                if (audioDevice == null)
                {
                    Console.WriteLine("No capture devices found, attempting to use default device...");
                }
                
                if (audioDevice != null)
                {
                    Console.WriteLine($"Using device: {audioDevice.Device} - {audioDevice.Name}");
                }
                else
                {
                    Console.WriteLine("Using default device");
                }
                
                // Test start/stop cycle 6 times
                for (int i = 0; i < 6; i++)
                {
                    Console.WriteLine($"\n--- Cycle {i + 1}/6 ---");
                    
                    // Create SDLCapture instance with the selected device
                    var capture = new SDLCapture(audioDevice);
                    
                    // Set the desired recording format
                    capture.WaveFormat = new WaveFormat(48000, 16, 2); // 44.1kHz, 16-bit, stereo
                    
                    bool dataReceived = false;
                    bool errorOccurred = false;
                    string errorMessage = "";
                    
                    // Subscribe to events
                    capture.DataAvailable += (sender, e) =>
                    {
                        if (e != null && e.BytesRecorded > 0)
                        {
                            Console.WriteLine($"  Captured {e.BytesRecorded} bytes");
                            dataReceived = true;
                        }
                    };
                    
                    capture.RecordingStopped += (sender, e) =>
                    {
                        if (e != null && e.Exception != null)
                        {
                            Console.WriteLine($"  Recording stopped with error: {e.Exception.Message}");
                            errorOccurred = true;
                            errorMessage = e.Exception.Message;
                        }
                        else
                        {
                            Console.WriteLine("  Recording stopped normally");
                        }
                    };
                    
                    Console.WriteLine($"  Starting recording cycle {i + 1}...");
                    capture.StartRecording();
                    
                    // Wait for 3 seconds to allow for more data capture
                    System.Threading.Thread.Sleep(3000);
                    
                    Console.WriteLine($"  Stopping recording cycle {i + 1}...");
                    capture.StopRecording();
                    
                    // Small delay between cycles
                    System.Threading.Thread.Sleep(1000);
                    
                    // Clean up
                    capture.Dispose();
                    
                    Console.WriteLine($"  Cycle {i + 1} completed. Data received: {dataReceived}, Error: {errorOccurred}");
                    
                    if (errorOccurred)
                    {
                        Console.WriteLine($"  Error message: {errorMessage}");
                    }
                }
                
                Console.WriteLine("\nSDLCapture start/stop test completed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during SDLCapture test: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}