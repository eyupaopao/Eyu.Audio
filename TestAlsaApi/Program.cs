using System;
using System.Threading;
using System.Threading.Tasks;
using Eyu.Audio.Alsa;
using NAudio.Wave;

namespace TestAlsaApi
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Testing ALSA API...");

            // Create basic sound device settings
            var settings = new SoundDeviceSettings
            {
                PlaybackDeviceName = "default",
                RecordingDeviceName = "default",
                MixerDeviceName = "default",
                RecordingSampleRate = 4100,
                RecordingChannels = 2,
                RecordingBitsPerSample = 16
            };

            // Create ALSAApi instance
            var alsaApi = new ALSAApi(settings);

            try
            {
                // Test playback functionality
                await TestPlayback(alsaApi);

                // Test recording functionality
                await TestRecording(alsaApi);

                // Test volume control
                await TestVolumeControl(alsaApi);

                Console.WriteLine("\nAll tests completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during testing: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                await alsaApi.Dispose();
            }
        }

        static async Task TestPlayback(ALSAApi alsaApi)
        {
            Console.WriteLine("\n--- Testing Playback ---");

            try
            {
                // Create a simple sine wave generator for testing
                var waveProvider = new SineWaveProvider32(440.0f, 44100, 2); // 440Hz, 4.1kHz, stereo

                // Create cancellation token for the test
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)); // 5 second test

                Console.WriteLine("Starting playback test (5 seconds)...");
                // Convert ISampleProvider to IWaveProvider for the ALSAApi
                var waveProvider16 = new NAudio.Wave.SampleProviders.SampleToWaveProvider16(waveProvider);
                alsaApi.Play(waveProvider16, cts.Token);

                // Wait for the playback to complete or timeout
                await Task.Delay(5000);

                Console.WriteLine("Playback test completed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Playback test failed: {ex.Message}");
            }
        }

        static async Task TestRecording(ALSAApi alsaApi)
        {
            Console.WriteLine("\n--- Testing Recording ---");

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)); // 5 second test

                Console.WriteLine("Starting recording test (5 seconds)...");
                
                // Variable to hold recorded data count
                int dataPacketCount = 0;
                
                // Start recording with callback for captured data
                alsaApi.Record(data =>
                {
                    Console.WriteLine($"Recorded data packet: {data.Length} bytes");
                    dataPacketCount++;
                }, cts.Token);

                // Wait for the recording to complete or timeout
                await Task.Delay(5000);

                Console.WriteLine($"Recording test completed. Received {dataPacketCount} data packets.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Recording test failed: {ex.Message}");
            }
        }

        static async Task TestVolumeControl(ALSAApi alsaApi)
        {
            Console.WriteLine("\n--- Testing Volume Control ---");

            try
            {
                // Test getting and setting playback volume
                var currentVolume = alsaApi.PlaybackVolume;
                Console.WriteLine($"Current playback volume: {currentVolume}");

                // Set a new volume and verify
                alsaApi.PlaybackVolume = 50;
                var newVolume = alsaApi.PlaybackVolume;
                Console.WriteLine($"Set playback volume to 50, current: {newVolume}");

                // Test mute functionality
                Console.WriteLine($"Current playback mute state: {alsaApi.PlaybackMute}");
                alsaApi.PlaybackMute = true;
                Console.WriteLine($"Set playback mute to true, current: {alsaApi.PlaybackMute}");
                alsaApi.PlaybackMute = false;
                Console.WriteLine($"Set playback mute to false, current: {alsaApi.PlaybackMute}");

                // Test getting and setting recording volume
                var recVolume = alsaApi.RecordingVolume;
                Console.WriteLine($"Current recording volume: {recVolume}");

                alsaApi.RecordingVolume = 75;
                var newRecVolume = alsaApi.RecordingVolume;
                Console.WriteLine($"Set recording volume to 75, current: {newRecVolume}");

                // Test recording mute functionality
                Console.WriteLine($"Current recording mute state: {alsaApi.RecordingMute}");
                alsaApi.RecordingMute = true;
                Console.WriteLine($"Set recording mute to true, current: {alsaApi.RecordingMute}");
                alsaApi.RecordingMute = false;
                Console.WriteLine($"Set recording mute to false, current: {alsaApi.RecordingMute}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Volume control test failed: {ex.Message}");
            }
        }
    }

    // Simple sine wave provider for testing playback
    public class SineWaveProvider32 : ISampleProvider
    {
        private readonly float frequency;
        private readonly int sampleRate;
        private readonly int channelCount;
        private readonly double phaseIncrement;
        private double phase = 0;
        private WaveFormat? waveFormat;

        public SineWaveProvider32(float frequency, int sampleRate, int channelCount)
        {
            this.frequency = frequency;
            this.sampleRate = sampleRate;
            this.channelCount = channelCount;
            this.phaseIncrement = 2 * Math.PI * frequency / sampleRate;
            this.waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channelCount);
        }

        public WaveFormat WaveFormat => waveFormat ?? WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

        public SineWaveProvider32() : this(440, 44100, 2) { }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesWritten = 0;
            for (int i = 0; i < count; i += channelCount)
            {
                float sampleValue = (float)Math.Sin(phase);
                phase += phaseIncrement;
                if (phase > 2 * Math.PI) phase -= 2 * Math.PI;

                for (int ch = 0; ch < channelCount; ch++)
                {
                    buffer[offset + i + ch] = sampleValue;
                    samplesWritten++;
                }
            }
            return samplesWritten;
        }
    }
}