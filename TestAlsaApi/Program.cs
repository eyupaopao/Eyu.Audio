using Eyu.Audio.Alsa;
using NAudio.Wave;

// Test the GetFormat function with various parameters
Console.WriteLine("Testing GetFormat function...");

// Create settings with high values that might not be supported
var settings = new SoundDeviceSettings
{
    RecordingSampleRate = 192000,  // Very high sample rate
    RecordingChannels = 8,         // Many channels
    RecordingBitsPerSample = 32,   // High bit depth
    PlaybackSampleRate = 192000,   // Very high sample rate
    PlaybackChannels = 8,          // Many channels
    PlaybackBitsPerSample = 32,    // High bit depth
    RecordingDeviceName = "default", // Use default device
    PlaybackDeviceName = "default"  // Use default device
};

var api = new ALSAApi(settings);

try 
{
    // Test getting capture format (should return recording settings or fallbacks)
    var captureFormat = api.GetFormat(true);
    Console.WriteLine($"Capture Format: {captureFormat.SampleRate} Hz, {captureFormat.Channels} channels, {captureFormat.BitsPerSample} bits");

    // Test getting playback format (should return playback settings or fallbacks)
    var playbackFormat = api.GetFormat(false);
    Console.WriteLine($"Playback Format: {playbackFormat.SampleRate} Hz, {playbackFormat.Channels} channels, {playbackFormat.BitsPerSample} bits");
}
catch (Exception ex)
{
    Console.WriteLine($"Exception occurred during GetFormat(true): {ex.Message}");
}

// Test with more reasonable settings
var settings2 = new SoundDeviceSettings
{
    RecordingSampleRate = 44100,   // Common sample rate
    RecordingChannels = 2,         // Stereo
    RecordingBitsPerSample = 16,   // Common bit depth
    PlaybackSampleRate = 48000,    // Common sample rate
    PlaybackChannels = 2,          // Stereo
    PlaybackBitsPerSample = 16,    // Common bit depth
    RecordingDeviceName = "default",
    PlaybackDeviceName = "default"
};

var api2 = new ALSAApi(settings2);

try 
{
    // Test getting capture format with reasonable settings
    var captureFormat2 = api2.GetFormat(true);
    Console.WriteLine($"Capture Format 2: {captureFormat2.SampleRate} Hz, {captureFormat2.Channels} channels, {captureFormat2.BitsPerSample} bits");

    // Test getting playback format with reasonable settings
    var playbackFormat2 = api2.GetFormat(false);
    Console.WriteLine($"Playback Format 2: {playbackFormat2.SampleRate} Hz, {playbackFormat2.Channels} channels, {playbackFormat2.BitsPerSample} bits");
}
catch (Exception ex)
{
    Console.WriteLine($"Exception occurred during GetFormat with reasonable settings: {ex.Message}");
}

// Test with extremely high values that are very unlikely to be supported
var settings3 = new SoundDeviceSettings
{
    RecordingSampleRate = 384000, // Extremely high sample rate
    RecordingChannels = 16,        // Very many channels
    RecordingBitsPerSample = 64,   // Very high bit depth
    PlaybackSampleRate = 384000,   // Extremely high sample rate
    PlaybackChannels = 16,         // Very many channels
    PlaybackBitsPerSample = 64,    // Very high bit depth
    RecordingDeviceName = "default",
    PlaybackDeviceName = "default"
};

var api3 = new ALSAApi(settings3);

try 
{
    // Test getting capture format with extreme settings
    var captureFormat3 = api3.GetFormat(true);
    Console.WriteLine($"Capture Format 3 (extreme): {captureFormat3.SampleRate} Hz, {captureFormat3.Channels} channels, {captureFormat3.BitsPerSample} bits");

    // Test getting playback format with extreme settings
    var playbackFormat3 = api3.GetFormat(false);
    Console.WriteLine($"Playback Format 3 (extreme): {playbackFormat3.SampleRate} Hz, {playbackFormat3.Channels} channels, {playbackFormat3.BitsPerSample} bits");
}
catch (Exception ex)
{
    Console.WriteLine($"Exception occurred during GetFormat with extreme settings: {ex.Message}");
}

Console.WriteLine("Test completed.");