using Eyu.Audio.Recorder;
using Eyu.Audio.Utils;
using NAudio.Wave;
using System;
using System.IO;
using System.Diagnostics;

namespace Sample
{
    public class AlsaCaptureTest
    {
        public static void TestAlsaCapture()
        {
            Console.WriteLine("Testing ALSA Capture...");
            
            try
            {
                // Create AlsaCapture instance with default settings
                var capture = new AlsaCapture();
                
                // Set the desired recording format
                capture.WaveFormat = new WaveFormat(44100, 16, 2); // 44.1kHz, 16-bit, stereo
                
                // Subscribe to events
                capture.DataAvailable += (sender, e) =>
                {
                    Console.WriteLine($"Captured {e.BytesRecorded} bytes");
                };
                
                capture.RecordingStopped += (sender, e) =>
                {
                    Console.WriteLine($"Recording stopped. Exception: {e.Exception?.Message}");
                };
                
                Console.WriteLine("Starting recording for 10 seconds...");
                Console.WriteLine("Press any key to stop early...");
                
                // Start recording
                capture.StartRecording();
                
                // Wait for 10 seconds or until key press
                for (int i = 0; i < 10; i++)
                {
                    if (Console.KeyAvailable)
                    {
                        Console.ReadKey();
                        break;
                    }
                    System.Threading.Thread.Sleep(1000);
                    Console.WriteLine($"{10 - i} seconds remaining...");
                }
                
                // Stop recording
                Console.WriteLine("Stopping recording...");
                capture.StopRecording();
                
                Console.WriteLine("ALSA Capture test completed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during ALSA capture test: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
        
        public static void TestAlsaCaptureToFile()
        {
            Console.WriteLine("Testing ALSA Capture to file...");
            
            try
            {
                // Create AlsaCapture instance
                var capture = new AlsaCapture();
                capture.WaveFormat = new WaveFormat(44100, 16, 2);
                
                string fileName = "test_alsa_capture.wav";
                
                Console.WriteLine($"Starting recording to file: {fileName}");
                Console.WriteLine("Recording for 10 seconds...");
                
                // Record directly to file
                capture.StartRecording(fileName);
                
                // Wait for 10 seconds
                System.Threading.Thread.Sleep(10000);
                
                // Stop recording
                capture.StopRecording();
                
                Console.WriteLine($"Recording completed. File saved as: {fileName}");
                
                // Check if file exists and its size
                if (File.Exists(fileName))
                {
                    var fileInfo = new FileInfo(fileName);
                    Console.WriteLine($"File size: {fileInfo.Length} bytes");
                    
                    // Verify it's a valid WAV file by checking header
                    using (var fs = new FileStream(fileName, FileMode.Open))
                    {
                        byte[] header = new byte[12];
                        int read = fs.Read(header, 0, 12);
                        if (read >= 12)
                        {
                            string riff = System.Text.Encoding.ASCII.GetString(header, 0, 4);
                            if (riff == "RIFF")
                            {
                                Console.WriteLine("Valid WAV file detected.");
                            }
                            else
                            {
                                Console.WriteLine("File may not be a valid WAV file.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during ALSA capture to file test: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
        
        public static void ListAlsaDevices()
        {
            Console.WriteLine("Listing ALSA devices using the new AlsaDeviceEnumerator...");
            
            try
            {
                var captureDevices = Eyu.Audio.Utils.AlsaDeviceEnumerator.GetCaptureDevices();
                Console.WriteLine($"Found {captureDevices.Count} capture devices:");
                
                foreach (var device in captureDevices)
                {
                    Console.WriteLine($"  - {device.Device}: {device.Name}");
                }
                
                var playbackDevices = Eyu.Audio.Utils.AlsaDeviceEnumerator.GetPlaybackDevices();
                Console.WriteLine($"\nFound {playbackDevices.Count} playback devices:");
                
                foreach (var device in playbackDevices)
                {
                    Console.WriteLine($"  - {device.Device}: {device.Name}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not list ALSA devices: {ex.Message}");
                Console.WriteLine("Falling back to system commands...");
                
                // Fallback to original method
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "bash",
                        Arguments = "-c \"arecord -L\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using (var process = Process.Start(startInfo))
                    {
                        string output = process?.StandardOutput.ReadToEnd();
                        string error = process?.StandardError.ReadToEnd();
                        
                        process?.WaitForExit();
                        
                        if (!string.IsNullOrEmpty(output))
                        {
                            Console.WriteLine("Available ALSA capture devices:");
                            Console.WriteLine(output);
                        }
                        else if (!string.IsNullOrEmpty(error))
                        {
                            Console.WriteLine($"Error listing devices: {error}");
                        }
                        else
                        {
                            Console.WriteLine("No output from device listing command.");
                        }
                    }
                }
                catch (Exception fallbackEx)
                {
                    Console.WriteLine($"Fallback also failed: {fallbackEx.Message}");
                }
            }
        }
        
        public static void TestAlsaCaptureWithDevice(string deviceName)
        {
            Console.WriteLine($"Testing ALSA Capture with specific device: {deviceName}");
            
            try
            {
                // Create AudioDevice with the specified device name
                var audioDevice = new AudioDevice 
                { 
                    Device = deviceName, 
                    IsCapture = true,
                    Name = deviceName
                };
                
                // Create AlsaCapture with specific device
                var capture = new AlsaCapture(audioDevice);
                capture.WaveFormat = new WaveFormat(48000, 16, 2); // Higher quality
                
                bool dataReceived = false;
                int totalBytes = 0;
                
                capture.DataAvailable += (sender, e) =>
                {
                    Console.WriteLine($"Captured {e.BytesRecorded} bytes from {deviceName}");
                    dataReceived = true;
                    totalBytes += e.BytesRecorded;
                };
                
                capture.RecordingStopped += (sender, e) =>
                {
                    Console.WriteLine($"Recording stopped for {deviceName}. Error: {e.Exception?.Message}");
                };
                
                Console.WriteLine($"Starting recording for 5 seconds...");
                capture.StartRecording();
                
                // Wait for 5 seconds
                System.Threading.Thread.Sleep(5000);
                
                capture.StopRecording();
                
                if (dataReceived)
                {
                    Console.WriteLine($"Successfully captured audio from device '{deviceName}'. Total bytes: {totalBytes}");
                }
                else
                {
                    Console.WriteLine($"No audio data received from device '{deviceName}'. Check if the device is working properly.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error testing device '{deviceName}': {ex.Message}");
            }
        }
    }
}