using NAudio.Lame;
using NAudio.MediaFoundation;
using NAudio.Wave;
using NLayer;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Eyu.Audio.Utils;

public static class MediaConverter
{
    public static bool MediaToMp3(WaveStream reader, string outPutFilePath)
    {
        try
        {
            MediaFoundationApi.Startup();
            MediaFoundationEncoder.EncodeToMp3(reader, outPutFilePath);
            MediaFoundationApi.Shutdown();
            return true;
        }
        catch { return false; }

    }
    public static bool ConvertWavToMp3(WaveStream reader, string mp3FilePath)
    {
        try
        {
            using var writer = new LameMP3FileWriter(mp3FilePath, reader.WaveFormat, LAMEPreset.ABR_320);
            reader.CopyTo(writer);
            return true;
        }
        catch 
        {
            return false; 
        }
    }

    public static async Task<bool> ConvertWavToMp3Async(WaveStream reader, string path, CancellationToken token)
    {
        try
        {
            using var writer = new LameMP3FileWriter(path, reader.WaveFormat, LAMEPreset.ABR_320);
            await reader.CopyToAsync(writer, token);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
